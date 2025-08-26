using Microsoft.UI.Xaml.Controls;

namespace SHCNRSample
{
	public sealed partial class MainPage : Page
	{
		public MainPageViewModel ViewModel { get; } = new();

		public MainPage()
		{
			InitializeComponent();
		}
	}
}
