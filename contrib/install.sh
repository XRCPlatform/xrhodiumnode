#!/bin/bash

cp brhodiumnode.service /etc/systemd/system/

currentdir=pwd
ln -s `pwd`/brhodiumnode /usr/bin/brhodiumnode
mkdir -p /usr/share/brhodiumnode
cd ../../build/bin/Debug/netcoreapp2.0/
ln -s `pwd`/BRhodium.BitcoinD.dll /usr/share/brhodiumnode/BRhodium.BitcoinD.dll
cd $currentdir

systemctl enable brhodiumnode.service
