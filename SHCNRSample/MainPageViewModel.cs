// Copyright (c) 0x5BFA.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Data;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace SHCNRSample
{
	public unsafe class MainPageViewModel : ObservableObject
	{
		private ObservableCollection<ChangeLogGroupItem> GroupedItems { get; set; } = [];
		public CollectionViewSource? ItemsGroupableSource { get => field; set => SetProperty(ref field, value); }

		public MainPageViewModel()
		{
  			ItemsGroupableSource = new CollectionViewSource
			{
				IsSourceGrouped = true,
				Source = GroupedItems
			};

			var groupedItem1 = new ChangeLogGroupItem() { Key = "FolderName1" };
			groupedItem1.Add(new ChangeLogItem() { TargetDisplayName = "DisplayName1", NotificationReason = "Reason1" });

			GroupedItems.Add(groupedItem1);

			SetupWatcher();
		}

		private void SetupWatcher()
		{
			using var watcher = FolderWatcher.Create("C:\\Users\\onein\\OneDrive\\Desktop", DoAction)
				?? throw new InvalidOperationException("Failed to create a folder watcher.");

			watcher.StartWatching(SHCNE_ID.SHCNE_ALLEVENTS);
		}


		private void DoAction(FolderWatcher @this, SHCNE_ID lEvent, nint psiPtr1, nint psiPtr2)
		{
			PWSTR pwszFullPath1 = null, pwszFullPath2 = null;
			if (psiPtr1 != nint.Zero)
				((IShellItem*)psiPtr1)->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &pwszFullPath1);
			if (psiPtr2 != nint.Zero)
				((IShellItem*)psiPtr2)->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &pwszFullPath2);

			if (lEvent is SHCNE_ID.SHCNE_RENAMEITEM or SHCNE_ID.SHCNE_RENAMEFOLDER)
			{
				Debug.WriteLine($"{@this.GetEventName(lEvent)}: {pwszFullPath1} => {pwszFullPath2}");
			}
			else
			{
				Debug.WriteLine($"{@this.GetEventName(lEvent)}: {pwszFullPath1} , {pwszFullPath2}");
			}

			PInvoke.CoTaskMemFree(pwszFullPath1);
			PInvoke.CoTaskMemFree(pwszFullPath2);

			if (psiPtr1 != nint.Zero) ((IShellItem*)psiPtr1)->Release();
			if (psiPtr2 != nint.Zero) ((IShellItem*)psiPtr2)->Release();
		}
	}
}
