using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace SHCNRSample
{
	public partial class App : Application
	{
		public static MainWindow? Window { get; set; }

		public App()
		{
			InitializeComponent();

			PInvoke.CoInitializeEx(COINIT.COINIT_APARTMENTTHREADED | COINIT.COINIT_DISABLE_OLE1DDE);
		}

		protected override void OnLaunched(LaunchActivatedEventArgs args)
		{
			Window = new MainWindow();
			Window.Activate();
		}

		~App()
		{
			PInvoke.CoUninitialize();
		}
	}
}
