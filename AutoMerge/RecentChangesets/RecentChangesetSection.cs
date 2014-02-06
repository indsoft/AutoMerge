﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoMerge.Base;
using AutoMerge.Events;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Events;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace AutoMerge
{
	[TeamExplorerSection(SectionId, AutoMergePage.PageId, 10)]
	public class RecentChangesetSection : TeamExplorerBaseSection
	{
		#region Members

		public const string SectionId = "8DA59790-3996-465E-A13F-27D64B3C2A9D";

		#endregion

		private readonly IEventAggregator _eventAggregator;

		/// <summary>
		/// Constructor.
		/// </summary>
		public RecentChangesetSection()
		{
			Title = Resources.RecentChangesetSectionName;
			IsVisible = true;
			IsExpanded = true;
			IsBusy = false;
			var view = new RecentChangesetsView();
			view.ParentSection = this;
			SectionContent = view;


			_eventAggregator = EventAggregatorFactory.Get();
			_eventAggregator.GetEvent<MergeCompleteEvent>()
				.Subscribe(OnMergeComplete);

			ViewChangesetDetailsCommand = new DelegateCommand(OnViewChangesetDetails, CanViewChangesetDetails);
		}

		private async void OnMergeComplete(bool obj)
		{
			await RefreshAsync();
		}

		/// <summary>
		/// Get the view.
		/// </summary>
		protected RecentChangesetsView View
		{
			get { return SectionContent as RecentChangesetsView; }
		}

		private Changeset _selectedChangeset;
		public Changeset SelectedChangeset
		{
			get
			{
				return _selectedChangeset;
			}
			set
			{
				_selectedChangeset = value;
				RaisePropertyChanged(() => SelectedChangeset);
				_eventAggregator.GetEvent<SelectChangesetEvent>().Publish(value.ChangesetId);
			}
		}

		/// <summary>
		/// List of changesets.
		/// </summary>
		public ObservableCollection<Changeset> Changesets
		{
			get
			{
				return _changesets;
			}
			protected set
			{
				_changesets = value;
				RaisePropertyChanged(() => Changesets);
			}
		}
		private ObservableCollection<Changeset> _changesets = new ObservableCollection<Changeset>();

		public ICommand ViewChangesetDetailsCommand { get; private set; }

		private string BaseTitle { get; set; }

		/// <summary>
		/// Initialize override.
		/// </summary>
		public async override void Initialize(object sender, SectionInitializeEventArgs e)
		{
			base.Initialize(sender, e);

			// Save off the base title that was set during the ctor
			BaseTitle = Title;

			// If the user navigated back to this page, there could be saved context
			// info that is passed in
			if (e.Context != null && e.Context is ChangesSectionContext)
			{
				// Restore the context instead of refreshing
				var context = (ChangesSectionContext)e.Context;
				Changesets = context.Changesets;
				SelectedChangeset = context.SelectedChangeset;
			}
			else
			{
				// Kick off the initial refresh
				await RefreshAsync();
			}
		}

		/// <summary> 
		/// Save contextual information about the current section state. 
		/// </summary> 
		public override void SaveContext(object sender, SectionSaveContextEventArgs e)
		{
			base.SaveContext(sender, e);

			// Save our current changeset list, selected item, and topmost item 
			// so they can be quickly restored when the user navigates back to 
			// the page 
			var context = new ChangesSectionContext
			{
				Changesets = Changesets,
				SelectedChangeset = SelectedChangeset
			};

			e.Context = context;
		}

		/// <summary>
		/// Refresh override.
		/// </summary>
		public async override void Refresh()
		{
			base.Refresh();
			await RefreshAsync();
		}

		private bool CanViewChangesetDetails()
		{
			return SelectedChangeset != null;
		}

		/// <summary>
		/// View details for the changeset.
		/// </summary>
		private void OnViewChangesetDetails()
		{
			var changesetId = SelectedChangeset.ChangesetId;

			var teamExplorer = GetService<ITeamExplorer>();
			if (teamExplorer != null)
			{
				teamExplorer.NavigateToPage(new Guid(TeamExplorerPageIds.ChangesetDetails), changesetId);
			}
		}

		/// <summary>
		/// ContextChanged override.
		/// </summary>
		protected override async void ContextChanged(object sender, ContextChangedEventArgs e)
		{
			base.ContextChanged(sender, e);

			// If the team project collection or team project changed, refresh 
			// the data for this section 
			if (e.TeamProjectCollectionChanged || e.TeamProjectChanged)
			{
				await RefreshAsync();
			}
		}

		/// <summary>
		/// Refresh the changeset data asynchronously.
		/// </summary>
		private async Task RefreshAsync()
		{
			try
			{
				// Set our busy flag and clear the previous data
				IsBusy = true;
				Changesets.Clear();

				ICollection<Changeset> changesets = new List<Changeset>();
				var context = CurrentContext;
				if (context != null && context.HasCollection && context.HasTeamProject)
				{
					var vcs = context.TeamProjectCollection.GetService<VersionControlServer>();
					if (vcs != null)
					{
						var changesetService = new ChangesetService(vcs, context.TeamProjectName);
						changesets = await changesetService.GetUserChangesets(vcs.AuthorizedUser);
					}
				}

				Changesets = new ObservableCollection<Changeset>(changesets);
				Title = Changesets.Count > 0
					? string.Format("{0} ({1})", BaseTitle, Changesets.Count)
					: BaseTitle;

				if (Changesets.Count > 0)
					SelectedChangeset = Changesets[0];
			}
			catch (Exception ex)
			{
				ShowNotification(ex.Message, NotificationType.Error);
			}
			finally
			{
				// Always clear our busy flag when done 
				IsBusy = false;
			}
		}

		/// <summary>
		/// Class to preserve the contextual information for this section.
		/// </summary>
		private class ChangesSectionContext
		{
			public ObservableCollection<Changeset> Changesets { get; set; }
			public Changeset SelectedChangeset { get; set; }
		}
	}
}