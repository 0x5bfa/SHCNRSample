// Copyright (c) 0x5BFA.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Data;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SHCNRSample
{
	public unsafe class MainPageViewModel : ObservableObject
	{
		private ObservableCollection<ChangeLogGroupItem> GroupedItems { get; set; } = [];
		public CollectionViewSource? ItemsGroupableSource { get => field; set => SetProperty(ref field, value); }

		private const uint c_notifyMessage = PInvoke.WM_USER + 200;

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		public delegate LRESULT WNDPROC(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);

		WNDPROC _wndProc = null!;

		Dictionary<HWND, FolderWatcher> Watchers = [];

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
			using ComPtr<IShellItem> pShellItem = default;
			fixed (char* pszPath = "C:\\Users\\onein\\OneDrive\\Desktop")
				PInvoke.SHCreateItemFromParsingName(pszPath, null, IID.IID_IShellItem, (void**)pShellItem.GetAddressOf());

			HWND hwnd = default;

			fixed (char* pszClassName = $"FolderWatcherWindowClass{Guid.NewGuid():B}")
			{
				_wndProc = new WNDPROC(WndProc);

				WNDCLASSEXW wndClass = default;
				wndClass.cbSize = (uint)sizeof(WNDCLASSEXW);
				wndClass.lpfnWndProc = (delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT>)Marshal.GetFunctionPointerForDelegate(_wndProc);
				wndClass.hInstance = PInvoke.GetModuleHandle(default(PWSTR));
				wndClass.lpszClassName = pszClassName;

				PInvoke.RegisterClassEx(&wndClass);
				hwnd = PInvoke.CreateWindowEx(0, pszClassName, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, default, wndClass.hInstance, null);
			}

			var watcher = new FolderWatcher();
			watcher.StartWatching(pShellItem.Get(), hwnd, c_notifyMessage, (long)SHCNE_ID.SHCNE_ALLEVENTS, false);

			Watchers.Add(hwnd, watcher);
		}

		private LRESULT WndProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
		{
			switch (uMsg)
			{
				case c_notifyMessage:
					Watchers.TryGetValue(hwnd, out var watcher);
					watcher?.OnChangeMessage(wParam, lParam);
					break;
				default:
					return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);
			}

			return (LRESULT)0;
		}
	}
}
