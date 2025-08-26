using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SHCNRSample
{
	public sealed partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			ExtendsContentIntoTitleBar = true;

			var frame = new Frame();
			Content = frame;
			frame.Navigate(typeof(MainPage));
		}
	}
}
