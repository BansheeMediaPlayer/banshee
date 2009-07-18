ASSEMBLY = Banshee.Paas
TARGET = library
LINK = $(REF_EXTENSION_PAAS)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.Paas/Banshee.Paas.Aether/AetherClient.cs \
	Banshee.Paas/Banshee.Paas.Aether/AetherClientID.cs \
	Banshee.Paas/Banshee.Paas.Aether/AetherClientState.cs \
	Banshee.Paas/Banshee.Paas.Aether/AetherClientStateChangedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/AetherAsyncCompletedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/AetherDelta.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/HttpMethod.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/MiroGuideClient.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/RequestState.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/ServiceMethodFlags.cs \
	Banshee.Paas/Banshee.Paas.Aether/OpmlParser.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelUpdateCompletedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelUpdateManager.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelUpdateStatus.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelUpdateTask.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ItemEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ItmsPodcast.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/RssParser.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/SyndicationClient.cs \
	Banshee.Paas/Banshee.Paas.Data/DownloadPreference.cs \
	Banshee.Paas/Banshee.Paas.Data/DownloadStatusFilterModel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasChannel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasChannelModel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasItem.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasTrackInfo.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasTrackListModel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasUnheardFilterModel.cs \
	Banshee.Paas/Banshee.Paas.DownloadManager.Gui/DownloadListModel.cs \
	Banshee.Paas/Banshee.Paas.DownloadManager.Gui/DownloadListView.cs \
	Banshee.Paas/Banshee.Paas.DownloadManager.Gui/DownloadManagerInterface.cs \
	Banshee.Paas/Banshee.Paas.DownloadManager.Gui/DownloadSource.cs \
	Banshee.Paas/Banshee.Paas.DownloadManager.Gui/DownloadSourceContents.cs \
	Banshee.Paas/Banshee.Paas.DownloadManager.Gui/DownloadUserJob.cs \
	Banshee.Paas/Banshee.Paas.DownloadManager/PaasDownloadManager.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellChannel.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellDownloadStatus.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellPaasStatusIndicator.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellPublished.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellUnheard.cs \
	Banshee.Paas/Banshee.Paas.Gui/Dialogs/ChannelPropertiesDialog.cs \
	Banshee.Paas/Banshee.Paas.Gui/Dialogs/DownloadPreferenceComboBox.cs \
	Banshee.Paas/Banshee.Paas.Gui/Dialogs/SubscribeDialog.cs \
	Banshee.Paas/Banshee.Paas.Gui/DownloadStatusFilterView.cs \
	Banshee.Paas/Banshee.Paas.Gui/IColumnCellDataHelper.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasActions.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasChannelView.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasColumnController.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasItemPage.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasItemView.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasSourceContents.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasUnheardFilterView.cs \
	Banshee.Paas/Banshee.Paas.Utils/StringUtils.cs \
	Banshee.Paas/Banshee.Paas/PaasImageFetchJob.cs \
	Banshee.Paas/Banshee.Paas/PaasService.cs \
	Banshee.Paas/Banshee.Paas/PaasSource.cs

RESOURCES =  \
	Banshee.Paas.addin.xml \
	Resources/ActiveSourceUI.xml \
	Resources/GlobalUI.xml

if ENABLE_PAAS
include $(top_srcdir)/build/build.mk
else
EXTRA_DIST = $(SOURCES) $(RESOURCES)
endif

