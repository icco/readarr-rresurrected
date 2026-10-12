# Troubleshooting

If none of this helps, open an issue on
[GitHub Issues](https://github.com/ricetim/readarr-rresurrected/issues) with your logs.

## Logs

Readarr keeps its logs in `/config/logs` inside the container, and shows them under
**System → Logs**:

- `readarr.txt`: the normal log, which is enough for most questions.
- `readarr.debug.txt` and `readarr.trace.txt`: more detail, written only when you ask for it.

For a bug report, capture trace logs: set **Settings → General → Log Level** to **Trace**,
reproduce the problem, then download `readarr.trace.txt` from **System → Logs → Files**. Remove API
keys, passwords and anything else private before sharing it, and set the level back afterwards,
since trace logs grow quickly.

The bundled metadata service logs to the container's output (`docker logs <container>`) and, if
`BOOKINFO_LOG_DIR` points somewhere writable such as `/config/bookinfo-logs`, to files there too.

## Locked out of Readarr

If you've forgotten your password, switch authentication off long enough to set a new one:

1. Stop the container.
2. In `/config/config.xml`, change `<AuthenticationMethod>Forms</AuthenticationMethod>` to
   `<AuthenticationMethod>None</AuthenticationMethod>`. If you set it with the
   `READARR__AUTH__METHOD` environment variable instead, change it there.
3. Start the container and open Readarr. It asks you to choose a new username and password, and
   switches authentication back on when you save them.

## Invalid response from the metadata service

Searching for authors and books, and loading their details, goes through the metadata service
bundled in the container, which fetches from Goodreads. When it fails, searches show an error and
refreshes don't update anything. To narrow it down:

1. Check the container's output (`docker logs <container>`) for errors from `bookinfo`.
2. An HTTP 401 from Goodreads usually clears on its own: Goodreads rotates the key the service
   uses, and the service fetches the new one.
3. An HTTP 403 means Goodreads is blocking requests, usually because too many were made too
   quickly. Wait, and consider lowering `BOOKINFO_GR_RATE` (requests per second, default 3).
4. If you point Readarr at a different metadata server with `READARR_METADATA_URL`, check that
   server is reachable from inside the container. See [Metadata source](settings.md#metadata-source).

Adding a large author can take a few minutes: the first page of books appears quickly and the rest
fill in as they arrive.

## Database disk image is malformed

Readarr's database (`/config/readarr.db`) is damaged. The usual cause is keeping `/config` on a
network share (NFS or SMB): SQLite, the database Readarr uses, isn't safe over them. Keep `/config`
on a local disk.

To recover, restore a backup. Readarr makes them automatically; they're listed under
**System → Backup** and stored in `/config/Backups`. If Readarr won't start, stop the container,
replace `readarr.db` with the one from a backup archive, and start it again.

## Migrating from upstream Readarr or another fork

**From the original Readarr**, mount your existing `/config` folder and start the container: the
database upgrades in place on first start. Make a copy of the folder first.

**From another Readarr fork**, the same usually works, with one catch. Forks number their database
upgrades independently, so a fork can mark an upgrade number as done that means something
different here, and this fork then skips it. Readarr checks for the result at every start: if
anything is missing, **System → Status** shows
[Database is missing columns](health-checks.md#database-is-missing-columns). Keep the copy of your
old `/config` until you're sure everything works.

## Paths and permissions in Docker

Readarr only sees paths inside its container, and so does your download client inside its own.
Problems start when they see the same files at different paths: the download client reports
`/downloads/book` while Readarr finds it at `/data/downloads/book`.

The simplest fix is to mount the same host folder at the same path in both containers, for example
a host folder mounted as `/data` everywhere, with downloads in `/data/downloads` and your library
in `/data/books`. Then nothing needs translating. If that's not possible, add a
[remote path mapping](settings.md#remote-path-mappings).

Mounting downloads and the library as one folder also lets Readarr hardlink instead of copying:
hardlinks only work within a single filesystem and a single mount inside the container.

Readarr runs as the user set on the container (`1000:1000` in the provided compose file). That
user needs read access to your downloads and write access to your library folders.
