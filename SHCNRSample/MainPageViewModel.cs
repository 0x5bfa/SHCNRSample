// Copyright (c) 0x5BFA.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Data;
using System.Collections.ObjectModel;

namespace SHCNRSample
{
	public class MainPageViewModel : ObservableObject
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
		}
	}
}
