# Darktable Migrator
Simple tool to help migrate [darktable](https://www.darktable.org/) image storage to another location or OS

If you use darktable but want to move your image files to another location or you want to switch the OS for using darktable, this is a tool that helps with it.

The tool modifies the `library.db` database file of darktable, rewriting the paths stored within.

## `library.db` location

To modify the library, you need to find the database file and modify it with the tool.

**Warning:** Backup the original file first! Do not run the tool on your real and only copy of the library database!

The database file seems to be located under `C:\Users\username\AppData\Local\darktable\library.db` on Windows, and at `~/.config/darktable/library.db` on Linux.

## Examples

You use darktable on Linux, with files stored under `/home/joe/photos`. You want to migrate to Windows, now keeping the images under `P:\`. Run:

```
DarktableMigrator --from-prefix /home/joe/photos/ --to-prefix P:\ --to-unix library.db
```

Or, the reverse: You use darktable on Windows, with files stored under `P:\`. You want to migrate to Linux, now keeping the images under `/home/joe/photos`. Run:

```
DarktableMigrator --from-prefix P:\ --to-prefix /home/joe/photos/ --to-windows library.db
```

Or a smaller change. You want to migrate (some) photos from `C:\Users\Joe\Pictures` to `P:\`. Run:

```
DarktableMigrator --from-prefix C:\Users\Joe\Pictures\ --to-prefix P:\ library.db
```

## Command-line arguments

```
DarktableMigrator [options] library.db
  -f, --from-prefix    Directory prefix from which to migrate.
  -t, --to-prefix      Directory prefix to which to migrate.
  -w, --to-windows     Convert path syntax from Unix to Windows.
  -u, --to-unix        Convert path syntax from Windows to Unix.
  -v, --verbose        Set output to verbose messages.
  --help               Display this help screen.
  --version            Display version information.
```
