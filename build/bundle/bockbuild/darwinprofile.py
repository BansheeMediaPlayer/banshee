import os
import shutil
from plistlib import Plist
from util import *
from unixprofile import UnixProfile

class DarwinProfile (UnixProfile):
	def __init__ (self):
		UnixProfile.__init__ (self)
		
		self.name = 'darwin'
		self.mac_sdk_path = '/Developer/SDKs/MacOSX10.5.sdk'
		
		if not os.path.isdir (self.mac_sdk_path):
			raise IOError ('Mac OS X SDK does not exist: %s' \
				% self.mac_sdk_path)

		self.gcc_arch_flags = [ '-m32', '-arch i386' ]
		self.gcc_flags.extend ([
			'-D_XOPEN_SOURCE',
			'-isysroot %{mac_sdk_path}',
			'-mmacosx-version-min=10.5'
		])
		self.gcc_flags.extend (self.gcc_arch_flags)
		self.ld_flags.extend (self.gcc_arch_flags)

		self.env.set ('CC',  'gcc-4.2')
		self.env.set ('CXX', 'g++-4.2')

	def bundle (self):
		self.make_app_bundle ()

	def make_app_bundle (self):
		plist_path = os.path.join (self.bundle_skeleton_dir, 'Contents', 'Info.plist')
		app_name = 'Unknown.app'
		plist = None
		if os.path.exists (plist_path):
			plist = Plist.fromFile (plist_path)
			app_name = plist['CFBundleExecutable']
		else:
			print 'Warning: no Contents/Info.plist in .app skeleton'

		self.bundle_app_dir = os.path.join (self.bundle_output_dir, app_name + '.app')
		self.bundle_contents_dir = os.path.join (self.bundle_app_dir, 'Contents')
		self.bundle_res_dir = os.path.join (self.bundle_contents_dir, 'Resources')
		self.bundle_macos_dir = os.path.join (self.bundle_contents_dir, 'MacOS')

		# Create the .app tree, copying the skeleton
		shutil.rmtree (self.bundle_app_dir, ignore_errors = True)
		shutil.copytree (self.bundle_skeleton_dir, self.bundle_app_dir)
		if not os.path.exists (self.bundle_contents_dir): os.makedirs (self.bundle_contents_dir)
		if not os.path.exists (self.bundle_res_dir): os.makedirs (self.bundle_res_dir)
		if not os.path.exists (self.bundle_macos_dir): os.makedirs (self.bundle_macos_dir)

		# Generate the PkgInfo
		pkginfo_path = os.path.join (self.bundle_contents_dir, 'PkgInfo')
		if not os.path.exists (pkginfo_path) and not plist == None:
			fp = open (pkginfo_path, 'w')
			fp.write (plist['CFBundlePackageType'])
			fp.write (plist['CFBundleSignature'])
			fp.close ()

		# Run solitary against the installation to collect files
		files = ''
		for file in self.bundle_from_build:
			files = files + ' "%s"' % os.path.join (self.prefix, file)

		run_shell ('mono --debug solitary/Solitary.exe '
			'--mono-prefix="%s" --root="%s" --out="%s" %s' % \
			(self.prefix, self.prefix, self.bundle_res_dir, files))

	def configure_pango (self):
		pango_querymodules = os.path.join (self.prefix, 'bin', 'pango-querymodules')
		if not os.path.isfile (pango_querymodules):
			print 'Could not find pango-querymodules in the build root'
			return

		pango_dir = os.path.join (self.bundle_res_dir, 'etc', 'pango')
		if not os.path.exists (pango_dir):
			os.makedirs (pango_dir)

		fp = open (os.path.join (pango_dir, 'pango.modules'), 'w')
		for line in backtick (pango_querymodules):
			line = line.strip ()
			if line.startswith ('#'):
				continue
			elif line.startswith (self.prefix):
				line = line[len (self.prefix) + 1:]
			fp.write (line + '\n')
		fp.close ()

		fp = open (os.path.join (pango_dir, 'pangorc'), 'w')
		fp.write ('[Pango]\n')
		fp.write ('ModulesPath=./pango.modules\n')
		fp.close ()
