if HAVE_GNOME_DOC_UTILS
include $(top_srcdir)/build/gnome-doc-utils.make

DOC_ID = banshee
DOC_INCLUDES = legal.xml
DOC_FIGURES = \
	figures/main-window.png

DOC_PAGES = \
	amazon.page \
	extensions.page \
	import.page \
	index.page \
	introduction.page \
	keyboard-shortcuts.page \
	lastfm.page \
	manage-playlists.page \
	manage-tags.page \
	play-queue.page \
	play.page \
	podcast.page \
	radio.page \
	search.page \
	sort.page \
	sync.page \
	ui.page

DOC_LINGUAS = ca cs da de el en_GB es fr gl hu ja pt_BR sl uk zh_CN
dist-hook: doc-dist-hook

endif
