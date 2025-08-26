// Copyright (c) 0x5BFA.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace SHCNRSample
{
	public unsafe class MainPageViewModel : ObservableObject, IDisposable
	{
		private ObservableCollection<ChangeLogGroupItem> GroupedItems { get; set; } = [];
		public CollectionViewSource? ItemsGroupableSource { get => field; set => SetProperty(ref field, value); }

		public string? MessageText { get => field; set => SetProperty(ref field, value); }

		public ICommand AddFolderCommand;
		public ICommand WatchFolderRecursivelyCheckCommand;

		private FolderWatcher? _folderWatcher;
		private bool _needsToUpdateCollectionViewSource;
		private readonly DispatcherQueueTimer _timer;

		public MainPageViewModel()
		{
			MessageText = "Pick a folder to watch its change";

			AddFolderCommand = new RelayCommand(ExecuteAddFolderCommand);
			WatchFolderRecursivelyCheckCommand = new RelayCommand(ExecuteWatchFolderRecursivelyCheckCommand);

			// Add debounce timer to reduce UI updates in case there are many changes in a short time.
			var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
			_timer = dispatcherQueue.CreateTimer();
			_timer.Interval = TimeSpan.FromMilliseconds(1000);
			_timer.Tick += (s, e) => UpdateCollectionViewSource();
		}

		private void ExecuteAddFolderCommand()
		{
			_timer.Stop();

			using ComPtr<IFileDialog> pfd = default;
			HRESULT hr = PInvoke.CoCreateInstance(CLSID.CLSID_FileOpenDialog, null, CLSCTX.CLSCTX_INPROC_SERVER, IID.IID_IFileOpenDialog, (void**)pfd.GetAddressOf());
			if (SUCCEEDED(hr))
			{
				FILEOPENDIALOGOPTIONS dwOptions;
				if (SUCCEEDED(pfd.Get()->GetOptions(&dwOptions)))
					pfd.Get()->SetOptions(dwOptions | FILEOPENDIALOGOPTIONS.FOS_ALLNONSTORAGEITEMS | FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS);

				pfd.Get()->SetTitle("Pick an item to watch its change...");

				hr = pfd.Get()->Show((HWND)WinRT.Interop.WindowNative.GetWindowHandle(App.Window));
				if (SUCCEEDED(hr))
				{
					IShellItem* psi;
					hr = pfd.Get()->GetResult(&psi);
					if (SUCCEEDED(hr))
					{
						_folderWatcher = FolderWatcher.Create(psi, HandleFolderChangeEvents)
							?? throw new InvalidOperationException("Failed to create a folder watcher.");

						_folderWatcher.StartWatching(SHCNE_ID.SHCNE_ALLEVENTS);

						MessageText = "Started watching the picked folder...";
					}
				}
			}

			if (_folderWatcher is not null) _timer.Start();
		}

		private void ExecuteWatchFolderRecursivelyCheckCommand()
		{

		}

		private void HandleFolderChangeEvents(FolderWatcher @this, SHCNE_ID lEvent, nint psiPtr1, nint psiPtr2)
		{
			PWSTR pwszFullPath1 = null, pwszFullPath2 = null;
			if (psiPtr1 != nint.Zero)
				((IShellItem*)psiPtr1)->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &pwszFullPath1);
			if (psiPtr2 != nint.Zero)
				((IShellItem*)psiPtr2)->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &pwszFullPath2);

			AddChangeLogToListView(@this.GetTargetPath()!, @this.GetEventName(lEvent), new(pwszFullPath1), new(pwszFullPath2));

			PInvoke.CoTaskMemFree(pwszFullPath1);
			PInvoke.CoTaskMemFree(pwszFullPath2);

			if (psiPtr1 != nint.Zero) ((IShellItem*)psiPtr1)->Release();
			if (psiPtr2 != nint.Zero) ((IShellItem*)psiPtr2)->Release();
		}

		private void AddChangeLogToListView(string folderName, string? changeReason, string? target1, string? target2)
		{
			var logItem = new ChangeLogItem() { Target1DisplayName = target1, Target2DisplayName = string.IsNullOrEmpty(target2) ? "N/A" : target2, NotificationReason = changeReason };

			if (GroupedItems.Where(x => x.Key.Equals(folderName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault() is ChangeLogGroupItem groupedItem)
			{
				groupedItem.Add(logItem);
			}
			else
			{
				var groupedItem1 = new ChangeLogGroupItem() { Key = folderName };
				groupedItem1.Add(logItem);
				GroupedItems.Add(groupedItem1);
			}

			MessageText = null;
			_needsToUpdateCollectionViewSource = true;
		}

		private void UpdateCollectionViewSource()
		{
			if (_needsToUpdateCollectionViewSource)
			{
				ItemsGroupableSource = new()
				{
					IsSourceGrouped = true,
					Source = GroupedItems,
				};

				_needsToUpdateCollectionViewSource = false;
			}
		}

		public void Dispose()
		{
			_folderWatcher?.Dispose();
		}
	}
}
