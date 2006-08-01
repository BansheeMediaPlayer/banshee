
#include <nautilus-burn-recorder.h>

#ifdef HAVE_CONFIG_H
#include "config.h"
#endif /* HAVE_CONFIG_H */

struct NautilusBurnRecorderTrack *
nautilus_burn_glue_create_track (const char *filename,
				 NautilusBurnRecorderTrackType type)
{
	struct NautilusBurnRecorderTrack *track;

	track = g_new0 (typeof (struct NautilusBurnRecorderTrack), 1);

	track->type = type;
	
	if (type == NAUTILUS_BURN_RECORDER_TRACK_TYPE_AUDIO) {
		track->contents.audio.filename = g_strdup (filename);
	} else {
		track->contents.data.filename = g_strdup (filename);
	}

	return track;
}

char *
nautilus_burn_glue_drive_get_id (NautilusBurnDrive *drive)
{
#ifndef HAVE_LIBNAUTILUS_BURN_4
	return g_strdup (drive->cdrecord_id);
#else /* HAVE_LIBNAUTILUS_BURN_4 */
	return g_strdup (nautilus_burn_drive_get_device (drive));
#endif /* HAVE_LIBNAUTILUS_BURN_4 */
}

NautilusBurnDriveType
nautilus_burn_glue_drive_get_type (NautilusBurnDrive *drive)
{
#ifndef HAVE_LIBNAUTILUS_BURN_4
	return drive->type;
#else /* HAVE_LIBNAUTILUS_BURN_4 */
	return nautilus_burn_drive_get_drive_type (drive);
#endif /* HAVE_LIBNAUTILUS_BURN_4 */
}

char *
nautilus_burn_glue_drive_get_display_name (NautilusBurnDrive *drive)
{
#ifndef HAVE_LIBNAUTILUS_BURN_4
	return g_strdup (drive->display_name);
#else /* HAVE_LIBNAUTILUS_BURN_4 */
	return nautilus_burn_drive_get_name_for_display (drive);
#endif /* HAVE_LIBNAUTILUS_BURN_4 */
}

int
nautilus_burn_glue_drive_get_max_read_speed (NautilusBurnDrive *drive)
{
#ifndef HAVE_LIBNAUTILUS_BURN_4
	return drive->max_speed_read;
#else /* HAVE_LIBNAUTILUS_BURN_4 */
	return nautilus_burn_drive_get_max_speed_read (drive);
#endif /* HAVE_LIBNAUTILUS_BURN_4 */
}

int
nautilus_burn_glue_drive_get_max_write_speed (NautilusBurnDrive *drive)
{
#ifndef HAVE_LIBNAUTILUS_BURN_4
	return drive->max_speed_write;
#else /* HAVE_LIBNAUTILUS_BURN_4 */
	return nautilus_burn_drive_get_max_speed_write (drive);
#endif /* HAVE_LIBNAUTILUS_BURN_4 */
}

char *
nautilus_burn_glue_drive_get_device (NautilusBurnDrive *drive)
{
#ifndef HAVE_LIBNAUTILUS_BURN_4
	return g_strdup (drive->device);
#else /* HAVE_LIBNAUTILUS_BURN_4 */
	return g_strdup (nautilus_burn_drive_get_device (drive));
#endif /* HAVE_LIBNAUTILUS_BURN_4 */
}
