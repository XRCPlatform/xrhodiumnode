# BRhodium Node setup with systemd

To setup BRhodiumNode using systemd on Ubuntu:

 1. Modify `brhodiumnode.service` by adding the username that the service will run under.
 2. Run `sudo ./install.sh`. It will install a boot script in `/usr/bin/brhodiumnode`, build the software and put the build in `/usr/share/brhodiumnode`. It will then install the systemd script.
 3. To start BRhodiumNode: `sudo systemctl start brhodiumnode`.
 4. Using `journalctl` is useful for viewing logs (Logs are also in, for exmaple, `~/.brhodiumnode/brhodium/BRhodiumMain/Logs`.):
    a. View syslogs like `tail -f` with `sudo journalctl -f -u brhodiumnode`. Use `-n NUM` with the number of lines you wish to view before tailing the log.
