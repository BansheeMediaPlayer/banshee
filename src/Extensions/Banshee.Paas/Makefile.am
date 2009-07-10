ASSEMBLY = Banshee.Paas
TARGET = library
LINK = $(REF_EXTENSION_PAAS)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.Paas/Banshee.Paas.Aether/AetherClient.cs \
	Banshee.Paas/Banshee.Paas.Aether/AetherClientID.cs \
	Banshee.Paas/Banshee.Paas.Aether/AetherClientState.cs \
	Banshee.Paas/Banshee.Paas.Aether/AetherClientStateChangedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/DownloadPreference.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/AetherAsyncCompletedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/AetherDelta.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/HttpMethod.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/MiroGuideClient.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/RequestState.cs \
	Banshee.Paas/Banshee.Paas.Aether/MiroGuideClient/ServiceMethodFlags.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelUpdateCompletedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelUpdateManager.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ChannelUpdateTask.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/ItemEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/RssParser.cs \
	Banshee.Paas/Banshee.Paas.Aether/SyndicationClient/SyndicationClient.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasChannel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasChannelModel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasItem.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasTrackInfo.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasTrackListModel.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellChannel.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellPaasStatusIndicator.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellPublished.cs \
	Banshee.Paas/Banshee.Paas.Gui/Dialogs/DownloadPreferenceComboBox.cs \
	Banshee.Paas/Banshee.Paas.Gui/Dialogs/SubscribeDialog.cs \
	Banshee.Paas/Banshee.Paas.Gui/Download/DownloadSource.cs \
	Banshee.Paas/Banshee.Paas.Gui/Download/DownloadUserJob.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasActions.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasChannelView.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasColumnController.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasItemPage.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasItemView.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasSourceContents.cs \
	Banshee.Paas/Banshee.Paas.Utils/StringUtils.cs \
	Banshee.Paas/DownloadManagerInterface.cs \
	Banshee.Paas/PaasDownloadManager.cs \
	Banshee.Paas/PaasService.cs \
	Banshee.Paas/PaasSource.cs

RESOURCES =  \
	Banshee.Paas.addin.xml \
	Resources/ActiveSourceUI.xml \
	Resources/GlobalUI.xml

if ENABLE_PAAS
include $(top_srcdir)/build/build.mk
else
EXTRA_DIST = $(SOURCES) $(RESOURCES)
endif

