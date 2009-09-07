#!/bin/bash

DESKTOP_SOURCE="$1"
VERSION="$2"
UPDATE_MIME_FILE="$3"
MIMETYPES_FILE="$(basename "$DESKTOP_SOURCE" .in.in).mime"

echo "[Desktop Entry]"

(cat "$DESKTOP_SOURCE" && cat common.desktop) |
	sed "s/\@VERSION\@/${VERSION}/g"

if [[ -f "$MIMETYPES_FILE" ]]; then
	MIMETYPES=$(
		grep -vE '^[[:space:]]*(#.*|)$' "$MIMETYPES_FILE" | \
		LC_ALL=C sort | uniq | \
		awk '{printf $1 ";"}' | sed 's,;$,,'
	)

	echo "MimeType=$MIMETYPES;"

	if [[ "$UPDATE_MIME_FILE" == "yes" ]]; then
		(grep -E '^[[:space:]]*#' "$MIMETYPES_FILE";
			echo "$MIMETYPES" | sed 's,;,\n,g') > "$MIMETYPES_FILE".tmp
		mv "$MIMETYPES_FILE".tmp "$MIMETYPES_FILE"
	fi
fi

