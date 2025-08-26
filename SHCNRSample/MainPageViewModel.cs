// Copyright (c) 0x5BFA.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
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

		public bool FolderAdded { get => field; set => SetProperty(ref field, value); }
		public bool CanStartWatcher { get => field; set => SetProperty(ref field, value); }
		public bool CanStopWatcher { get => field; set => SetProperty(ref field, value); }

		public bool IsRecursive { get => field; set => SetProperty(ref field, value); }

		public ICommand AddFolderCommand;
		public ICommand RemoveFolderCommand;
		public ICommand StartWatcherCommand;
		public ICommand StopWatcherCommand;

		private static Dictionary<SHCNE_ID, (string, string, string?)>? _eventNames;

		private FolderWatcher? _folderWatcher;
		private bool _needsToUpdateCollectionViewSource;
		private readonly DispatcherQueueTimer _timer;

		public MainPageViewModel()
		{
			MessageText = "Pick a folder to watch its change";

			AddFolderCommand = new RelayCommand(ExecuteAddFolderCommand);
			RemoveFolderCommand = new RelayCommand(ExecuteRemoveFolderCommand);
			StartWatcherCommand = new RelayCommand(ExecuteStartWatcherCommand);
			StopWatcherCommand = new RelayCommand(ExecuteStopWatcherCommand);

			// Add debounce timer to reduce UI updates in case there are many changes in a short time.
			var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
			_timer = dispatcherQueue.CreateTimer();
			_timer.Interval = TimeSpan.FromMilliseconds(1000);
			_timer.Tick += (s, e) => UpdateCollectionViewSource();

			_eventNames = new()
			{
				[SHCNE_ID.SHCNE_RENAMEITEM] =		("SHCNE_RENAMEITEM",		"\uE8AC", "SystemFillColorNeutralBrush"),
				[SHCNE_ID.SHCNE_CREATE] =			("SHCNE_CREATE",			"\uE710", "SystemFillColorSuccessBrush"),
				[SHCNE_ID.SHCNE_DELETE] =			("SHCNE_DELETE",			"\uE74D", "SystemFillColorCriticalBrush"),
				[SHCNE_ID.SHCNE_MKDIR] =			("SHCNE_MKDIR",				"\uE710", "SystemFillColorSuccessBrush"),
				[SHCNE_ID.SHCNE_RMDIR] =			("SHCNE_RMDIR",				"\uE74D", "SystemFillColorCriticalBrush"),
				[SHCNE_ID.SHCNE_MEDIAINSERTED] =	("SHCNE_MEDIAINSERTED",		"\uE8B5", "SystemFillColorSuccessBrush"),
				[SHCNE_ID.SHCNE_MEDIAREMOVED] =		("SHCNE_MEDIAREMOVED",		"\uEA52", "SystemFillColorCriticalBrush"),
				[SHCNE_ID.SHCNE_DRIVEREMOVED] =		("SHCNE_DRIVEREMOVED",		"\uEA52", "SystemFillColorCriticalBrush"),
				[SHCNE_ID.SHCNE_DRIVEADD] =			("SHCNE_DRIVEADD",			"\uE8B5", "SystemFillColorSuccessBrush"),
				[SHCNE_ID.SHCNE_NETSHARE] =			("SHCNE_NETSHARE",			"\uE71B", "SystemFillColorSuccessBrush"),
				[SHCNE_ID.SHCNE_NETUNSHARE] =		("SHCNE_NETUNSHARE",		"\uE71B", "SystemFillColorCriticalBrush"),
				[SHCNE_ID.SHCNE_ATTRIBUTES] =		("SHCNE_ATTRIBUTES",		"\uE723", "SystemFillColorNeutralBrush"),
				[SHCNE_ID.SHCNE_UPDATEDIR] =		("SHCNE_UPDATEDIR",			"\uE70F", "SystemFillColorNeutralBrush"),
				[SHCNE_ID.SHCNE_UPDATEITEM] =		("SHCNE_UPDATEITEM",		"\uE70F", "SystemFillColorNeutralBrush"),
				[SHCNE_ID.SHCNE_SERVERDISCONNECT] =	("SHCNE_SERVERDISCONNECT",	"\uE8CD", "SystemFillColorCriticalBrush"),
				[SHCNE_ID.SHCNE_DRIVEADDGUI] =		("SHCNE_DRIVEADDGUI",		"\uE8B5", "SystemFillColorSuccessBrush"),
				[SHCNE_ID.SHCNE_RENAMEFOLDER] =		("SHCNE_RENAMEFOLDER",		"\uE8AC", "SystemFillColorNeutralBrush"),
				[SHCNE_ID.SHCNE_FREESPACE] =		("SHCNE_FREESPACE",			"\uEDA2", "SystemFillColorNeutralBrush"),
			};
		}

		private void ExecuteAddFolderCommand()
		{
			using ComPtr<IFileOpenDialog> pfd = default;
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
						_folderWatcher = FolderWatcher.Create(psi, HandleFolderChangeEvents, IsRecursive)
							?? throw new InvalidOperationException("Failed to create a folder watcher.");

						MessageText = "A folder was picked. Now you can start watching changes...";

						FolderAdded = true;
						CanStartWatcher = true;
						CanStopWatcher = false;
					}
				}
			}
		}

		private void ExecuteRemoveFolderCommand()
		{
			_folderWatcher?.StopWatching();
			_folderWatcher?.Dispose();
			_timer.Stop();

			FolderAdded = false;
			CanStartWatcher = true;
			CanStopWatcher = false;
		}

		private void ExecuteStartWatcherCommand()
		{
			_folderWatcher?.StartWatching(SHCNE_ID.SHCNE_ALLEVENTS);
			_timer.Start();

			CanStartWatcher = false;
			CanStopWatcher = true;

			MessageText = "Started watching the picked folder...";
		}

		private void ExecuteStopWatcherCommand()
		{
			_folderWatcher?.StartWatching(SHCNE_ID.SHCNE_ALLEVENTS);
			_timer.Start();

			CanStartWatcher = true;
			CanStopWatcher = false;
		}

		private void HandleFolderChangeEvents(FolderWatcher @this, SHCNE_ID lEvent, nint psiPtr1, nint psiPtr2)
		{
			PWSTR pwszFullPath1 = null, pwszFullPath2 = null;
			if (psiPtr1 != nint.Zero)
				((IShellItem*)psiPtr1)->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &pwszFullPath1);
			if (psiPtr2 != nint.Zero)
				((IShellItem*)psiPtr2)->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &pwszFullPath2);

			string folderName = @this.GetTargetPath()!;

			_eventNames!.TryGetValue(lEvent, out var eventNames);

			var logItem = new ChangeLogItem()
			{
				NotificationReason = eventNames.Item1,
				NotificationReasonGlyph = eventNames.Item2,
				NotificationReasonGlyphForeground = eventNames.Item3 is null ? (Brush)App.Current.Resources["TextFillColorPrimaryBrush"] : (Brush)App.Current.Resources[eventNames.Item3],
				Target1DisplayName = new(pwszFullPath1),
				Target2DisplayName = string.IsNullOrEmpty(new(pwszFullPath2)) ? "N/A" : new(pwszFullPath2),
			};

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

			PInvoke.CoTaskMemFree(pwszFullPath1);
			PInvoke.CoTaskMemFree(pwszFullPath2);

			if (psiPtr1 != nint.Zero) ((IShellItem*)psiPtr1)->Release();
			if (psiPtr2 != nint.Zero) ((IShellItem*)psiPtr2)->Release();
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
