ASSEMBLY = Migo2
TARGET = library
LINK = $(REF_MIGO2)
SOURCES =  \
	Migo2.Async/AsyncStateManager.cs \
	Migo2.Async/CommandQueue/CommandDelegate.cs \
	Migo2.Async/CommandQueue/CommandQueue.cs \
	Migo2.Async/CommandQueue/CommandWrapper.cs \
	Migo2.Async/CommandQueue/EventWrapper.cs \
	Migo2.Async/CommandQueue/ICommand.cs \
	Migo2.Async/Task/CancellationType.cs \
	Migo2.Async/Task/Task.cs \
	Migo2.Async/Task/TaskCompletedEventArgs.cs \
	Migo2.Async/Task/TaskEventArgs.cs \
	Migo2.Async/Task/TaskState.cs \
	Migo2.Async/Task/TaskStateChangedEventArgs.cs \
	Migo2.Async/Task/WaitableTask.cs \
	Migo2.Async/TaskGroup/EventArgs/GroupStatusChangedEventArgs.cs \
	Migo2.Async/TaskGroup/EventArgs/ManipulatedEventArgs.cs \
	Migo2.Async/TaskGroup/EventArgs/ReorderedEventArgs.cs \
	Migo2.Async/TaskGroup/EventArgs/TaskAddedEventArgs.cs \
	Migo2.Async/TaskGroup/EventArgs/TaskProgressChangedEventArgs.cs \
	Migo2.Async/TaskGroup/EventArgs/TaskRemovedEventArgs.cs \
	Migo2.Async/TaskGroup/GroupProgressManager.cs \
	Migo2.Async/TaskGroup/GroupStatusManager.cs \
	Migo2.Async/TaskGroup/TaskGroup.cs \
	Migo2.Async/TaskGroup/TaskGroup_Collection.cs \
	Migo2.Collections/Pair.cs \
	Migo2.DownloadService/DownloadStatusManager.cs \
	Migo2.DownloadService/DownloadTaskStatusUpdatedEventArgs.cs \
	Migo2.DownloadService/HttpDownloadGroup.cs \
	Migo2.DownloadService/HttpDownloadGroupStatusChangedEventArgs.cs \
	Migo2.DownloadService/HttpDownloadManager.cs \
	Migo2.DownloadService/HttpFileDownloadErrors.cs \
	Migo2.DownloadService/HttpFileDownloadTask.cs \
	Migo2.Net/AsyncWebClient/AsyncWebClient.cs \
	Migo2.Net/AsyncWebClient/DownloadDataCompletedEventArgs.cs \
	Migo2.Net/AsyncWebClient/DownloadProgressChangedEventArgs.cs \
	Migo2.Net/AsyncWebClient/DownloadStatus.cs \
	Migo2.Net/AsyncWebClient/DownloadStatusUpdatedEventArgs.cs \
	Migo2.Net/AsyncWebClient/DownloadStringCompletedEventArgs.cs \
	Migo2.Net/AsyncWebClient/RemoteFileModifiedException.cs \
	Migo2.Net/AsyncWebClient/TransferStatusManager.cs \
	Migo2.Net/AsyncWebClient/TransferStatusManager_Rate.cs \
	Migo2.Utils/Rfc822DateTime.cs \
	Migo2.Utils/UnitUtils.cs \
	Migo2.Utils/XmlUtils.cs

if ENABLE_PAAS
include $(top_srcdir)/build/build.mk
else
EXTRA_DIST = $(SOURCES) $(RESOURCES)
endif

