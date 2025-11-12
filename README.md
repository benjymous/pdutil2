# pdutil2

This is a drop-in replacement for the 'pdutil' tool included in the Windows version of the Playdate SDK, (based on the 3.0 version)

## Features

* Same basic functionality as the stock pdutil (on Windows)
* `install` command now only copies new or changed files, for faster updating
* `clean` command does a clean install (removing existing pdx files before copying)
* `cmd` command allows you to run a [https://github.com/cranksters/playdate-reverse-engineering/blob/main/usb/usb.md](serial USB) command directly from the shell (without having to set up serial comms, etc)
* `shell` launches you directly into a serial USB shell
* `screen` saves a screenshot locally to the filename provided

## Installation
Copy the pdutil2.exe into your %PLAYDATE_SDK_PATH%\bin\ folder, then change any scripts to call pdutil2 instead of pdutil

## Platform support

Currently pdutil2 is Windows only, due to use of Windows specific APIs to access serial ports and drive mountings.  This may be possible to change, if anyone wants to do a PR!  
