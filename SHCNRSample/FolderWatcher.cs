// Copyright (c) 0x5BFA.
// Licensed under the MIT License.

using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace SHCNRSample
{
	public unsafe class FolderWatcher
	{
		private uint _uRegister;

		public bool StartWatching(IShellItem* psi, HWND hwnd, uint uMsg, long lEvents, BOOL fRecursive)
		{
			StopWatching();

			ITEMIDLIST* pidlWatch;
			HRESULT hr = PInvoke.SHGetIDListFromObject((IUnknown*)psi, &pidlWatch);
			if (SUCCEEDED(hr))
			{
				SHChangeNotifyEntry entry = default;
				entry.pidl = pidlWatch;
				entry.fRecursive = fRecursive;

				const SHCNRF_SOURCE nSources = SHCNRF_SOURCE.SHCNRF_ShellLevel | SHCNRF_SOURCE.SHCNRF_InterruptLevel | SHCNRF_SOURCE.SHCNRF_NewDelivery;
				_uRegister = PInvoke.SHChangeNotifyRegister(hwnd, nSources, (int)lEvents, uMsg, 1, &entry);

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

		public void OnChangeMessage(WPARAM wParam, LPARAM lParam)
		{
			long lEvent;
			ITEMIDLIST** rgpidl;
			HANDLE hNotifyLock = PInvoke.SHChangeNotification_Lock((HANDLE)(nuint)wParam, (uint)(nint)lParam, &rgpidl, (int*)&lEvent);
			if (!hNotifyLock.IsNull)
			{
				if (IsItemNotificationEvent(lEvent))
				{
					IShellItem* psi1 = null, psi2 = null;

					if (rgpidl[0] is not null)
					{
						PInvoke.SHCreateItemFromIDList(rgpidl[0], IID.IID_IShellItem, (void**)&psi1);
					}

					if (rgpidl[1] is not null)
					{
						PInvoke.SHCreateItemFromIDList(rgpidl[1], IID.IID_IShellItem, (void**)&psi2);
					}

					OnChangeNotify(lEvent, psi1, psi2);

					if (psi1 is not null) psi1->Release();
					if (psi2 is not null) psi2->Release();
				}
				else
				{
					// Dispatch non-item events here in the future
				}

				PInvoke.SHChangeNotification_Unlock(hNotifyLock);
			}
		}

		private void OnChangeNotify(long lEvent, IShellItem* psi1, IShellItem* psi2)
		{
			PWSTR pszLeft = null, pszRight = null;

			if (psi1 is not null)
			{
				psi1->GetDisplayName(SIGDN.SIGDN_PARENTRELATIVE, &pszLeft);
			}

			if (psi2 is not null)
			{
				psi2->GetDisplayName(SIGDN.SIGDN_PARENTRELATIVE, &pszRight);
			}

			if ((SHCNE_ID)lEvent is SHCNE_ID.SHCNE_RENAMEITEM or SHCNE_ID.SHCNE_RENAMEFOLDER)
			{
				Debug.WriteLine($"{pszLeft} => {pszRight}");
			}
			else
			{
				Debug.WriteLine($"{pszLeft} , {pszRight}");
			}

			PInvoke.CoTaskMemFree(pszLeft);
			PInvoke.CoTaskMemFree(pszRight);
		}

		private bool IsItemNotificationEvent(long lEvent)
		{
			return (lEvent & (long)(SHCNE_ID.SHCNE_UPDATEIMAGE | SHCNE_ID.SHCNE_ASSOCCHANGED | SHCNE_ID.SHCNE_EXTENDED_EVENT | SHCNE_ID.SHCNE_FREESPACE | SHCNE_ID.SHCNE_DRIVEADDGUI | SHCNE_ID.SHCNE_SERVERDISCONNECT)) is 0L;
		}
	}
}
