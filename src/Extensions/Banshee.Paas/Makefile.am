ASSEMBLY = Banshee.Paas
TARGET = library
LINK = $(REF_EXTENSION_PAAS)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.Paas/Banshee.Paas.Aether/AetherDelta.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/AetherAsyncCompletedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/AetherClient.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/AetherClientState.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/AetherClientStateChangedEventArgs.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/AsyncStateManager.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/HttpMethod.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/RequestState.cs \
	Banshee.Paas/Banshee.Paas.Aether/Client/ServiceMethodFlags.cs \
	Banshee.Paas/Banshee.Paas.Aether/Rfc822DateTime.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasChannel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasChannelModel.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasItem.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasTrackInfo.cs \
	Banshee.Paas/Banshee.Paas.Data/PaasTrackListModel.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellChannel.cs \
	Banshee.Paas/Banshee.Paas.Gui/ColumnCellPublished.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasActions.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasChannelView.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasItemView.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasSource.cs \
	Banshee.Paas/Banshee.Paas.Gui/PaasSourceContents.cs \
	Banshee.Paas/Banshee.Paas.Utils/StringUtils.cs \
	Banshee.Paas/PaasService.cs

RESOURCES =  \
	Banshee.Paas.addin.xml \
	Resources/ActiveSourceUI.xml \
	Resources/GlobalUI.xml

if ENABLE_PAAS
include $(top_srcdir)/build/build.mk
else
EXTRA_DIST = $(SOURCES) $(RESOURCES)
endif

