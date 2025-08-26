// Copyright (c) 0x5BFA.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;
using static SHCNRSample.MainPageViewModel;

namespace SHCNRSample
{
	[UnmanagedFunctionPointer(CallingConvention.Winapi)]
	public delegate LRESULT WNDPROC(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);

	public unsafe class FolderWatcher : IDisposable
	{
		private const uint c_uNotifyMessage = PInvoke.WM_USER + 200U;

		private static Dictionary<SHCNE_ID, string>? _eventNames;

		private uint _uRegister;
		private HWND _hwnd;
		WNDPROC? _wndProc;
		private IShellItem* _psi;
		private Action<FolderWatcher, SHCNE_ID, nint, nint>? _action;
		private bool _watchRecursively;

		private FolderWatcher() { } // Seal the default constructor

		public static FolderWatcher? Create(string targetPath, Action<FolderWatcher, SHCNE_ID, nint, nint> action, bool watchRecursively = false)
		{
			IShellItem* psi = null;
			fixed (char* pwszPath = targetPath)
				PInvoke.SHCreateItemFromParsingName(pwszPath, null, IID.IID_IShellItem, (void**)&psi);

			var watcher = new FolderWatcher() { _action = action, _psi = psi, _watchRecursively = watchRecursively };
			return watcher.InitializeWndProc() ? watcher : null;
		}

		public bool StartWatching(SHCNE_ID eventsToWatch)
		{
			StopWatching();

			ITEMIDLIST* pidlWatch;
			HRESULT hr = PInvoke.SHGetIDListFromObject((IUnknown*)_psi, &pidlWatch);
			if (SUCCEEDED(hr))
			{
				SHChangeNotifyEntry entry = default;
				entry.pidl = pidlWatch;
				entry.fRecursive = _watchRecursively;

				const SHCNRF_SOURCE nSources = SHCNRF_SOURCE.SHCNRF_ShellLevel | SHCNRF_SOURCE.SHCNRF_InterruptLevel | SHCNRF_SOURCE.SHCNRF_NewDelivery;
				_uRegister = PInvoke.SHChangeNotifyRegister(_hwnd, nSources, (int)eventsToWatch, c_uNotifyMessage, 1, &entry);

				PInvoke.CoTaskMemFree(pidlWatch);

				return true;
			}

			return false;
		}

		public void StopWatching()
		{
			if (_uRegister is not 0U)
			{
				PInvoke.SHChangeNotifyDeregister(_uRegister);
				_uRegister = 0U;
			}
		}

		public string? GetEventName(SHCNE_ID lEvent)
		{
			_eventNames ??= new()
			{
				[SHCNE_ID.SHCNE_RENAMEITEM] = "SHCNE_RENAMEITEM",
				[SHCNE_ID.SHCNE_CREATE] = "SHCNE_CREATE",
				[SHCNE_ID.SHCNE_DELETE] = "SHCNE_DELETE",
				[SHCNE_ID.SHCNE_MKDIR] = "SHCNE_MKDIR",
				[SHCNE_ID.SHCNE_RMDIR] = "SHCNE_RMDIR",
				[SHCNE_ID.SHCNE_MEDIAINSERTED] = "SHCNE_MEDIAINSERTED",
				[SHCNE_ID.SHCNE_MEDIAREMOVED] = "SHCNE_MEDIAREMOVED",
				[SHCNE_ID.SHCNE_DRIVEREMOVED] = "SHCNE_DRIVEREMOVED",
				[SHCNE_ID.SHCNE_DRIVEADD] = "SHCNE_DRIVEADD",
				[SHCNE_ID.SHCNE_NETSHARE] = "SHCNE_NETSHARE",
				[SHCNE_ID.SHCNE_NETUNSHARE] = "SHCNE_NETUNSHARE",
				[SHCNE_ID.SHCNE_ATTRIBUTES] = "SHCNE_ATTRIBUTES",
				[SHCNE_ID.SHCNE_UPDATEDIR] = "SHCNE_UPDATEDIR",
				[SHCNE_ID.SHCNE_UPDATEITEM] = "SHCNE_UPDATEITEM",
				[SHCNE_ID.SHCNE_SERVERDISCONNECT] = "SHCNE_SERVERDISCONNECT",
				[SHCNE_ID.SHCNE_DRIVEADDGUI] = "SHCNE_DRIVEADDGUI",
				[SHCNE_ID.SHCNE_RENAMEFOLDER] = "SHCNE_RENAMEFOLDER",
				[SHCNE_ID.SHCNE_FREESPACE] = "SHCNE_FREESPACE",
				[SHCNE_ID.SHCNE_UPDATEITEM] = "SHCNE_UPDATEITEM",
			};

			_eventNames.TryGetValue(lEvent, out var name);
			return name;
		}

		private bool InitializeWndProc()
		{
			_wndProc = new WNDPROC(WndProc);

			fixed (char* pszClassName = $"FolderWatcherWindowClass-{Guid.NewGuid():B}")
			{
				WNDCLASSEXW wndClass = default;
				wndClass.cbSize = (uint)sizeof(WNDCLASSEXW);
				wndClass.lpfnWndProc = (delegate* unmanaged[Stdcall]<HWND, uint, WPARAM, LPARAM, LRESULT>)Marshal.GetFunctionPointerForDelegate(_wndProc);
				wndClass.hInstance = PInvoke.GetModuleHandle(default(PWSTR));
				wndClass.lpszClassName = pszClassName;

				PInvoke.RegisterClassEx(&wndClass);
				_hwnd = PInvoke.CreateWindowEx(0, pszClassName, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, default, wndClass.hInstance, null);
			}

			return !_hwnd.IsNull;
		}

		private LRESULT WndProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
		{
			switch (uMsg)
			{
				case c_uNotifyMessage:
					OnFolderChangeMessageDispatched(wParam, lParam);
					break;
				default:
					return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);
			}

			return (LRESULT)0;
		}

		public void OnFolderChangeMessageDispatched(WPARAM wParam, LPARAM lParam)
		{
			SHCNE_ID lEvent;
			ITEMIDLIST** rgpidl;
			HANDLE hNotifyLock = PInvoke.SHChangeNotification_Lock((HANDLE)(nuint)wParam, (uint)(nint)lParam, &rgpidl, (int*)&lEvent);
			if (!hNotifyLock.IsNull)
			{
				if (IsItemNotificationEvent(lEvent))
				{
					IShellItem* psi1 = null, psi2 = null;
					if (rgpidl[0] is not null)
						PInvoke.SHCreateItemFromIDList(rgpidl[0], IID.IID_IShellItem, (void**)&psi1);
					if (rgpidl[1] is not null)
						PInvoke.SHCreateItemFromIDList(rgpidl[1], IID.IID_IShellItem, (void**)&psi2);

					// Call the action delegate to let the user of the watcher handle the event
					if (_action is not null)
						_action(this, lEvent, (nint)psi1, (nint)psi2);
				}
				else
				{
					// TODO: Dispatch non-item events here in the future
				}

				PInvoke.SHChangeNotification_Unlock(hNotifyLock);
			}
		}

		private bool IsItemNotificationEvent(SHCNE_ID lEvent)
		{
			return (lEvent & (SHCNE_ID.SHCNE_UPDATEIMAGE | SHCNE_ID.SHCNE_ASSOCCHANGED | SHCNE_ID.SHCNE_EXTENDED_EVENT | SHCNE_ID.SHCNE_FREESPACE | SHCNE_ID.SHCNE_DRIVEADDGUI | SHCNE_ID.SHCNE_SERVERDISCONNECT)) is 0L;
		}

		public void Dispose()
		{
			_wndProc = null; // Prevented GC from releasing the proc reference until this instance is disposed, but now it's fine to let it release this.
			_psi->Release();
		}
	}
}
