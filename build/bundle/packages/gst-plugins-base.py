configure_flags = [
	'--disable-gtk-doc',
	'--disable-gio',
	'--disable-gnome_vfs'
]

# FIXME: these should be passed on the Linux profile
# when we do away with xvideo/xoverlay and replace
# with Clutter and Cairo
if profile['name'] == 'osx':
	configure_flags.extend ([
		'--disable-x',
		'--disable-xvideo',
		'--disable-xshm'
	])

package = {
	'name':    'gst-plugins-base',
	'version': '0.10.25',
	'sources': [
		'http://gstreamer.freedesktop.org/src/%{name}/%{name}-%{version}.tar.gz'
	],
	'build': [
		'%{__configure} ' + ' '.join (configure_flags), 
		'%{__make}'
	]
}
