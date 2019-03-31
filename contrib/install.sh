#!/bin/bash

cp brhodiumnode.service /etc/systemd/system/

currentdir=`pwd`
ln -s `pwd`/brhodiumnode /usr/bin/brhodiumnode
mkdir -p /usr/share/brhodiumnode
pushd ..
bin/brhodium build
cp build/* /usr/share/brhodiumnode/
chmod +x /usr/share/brhodiumnode/BRhodium
cd $currentdir

systemctl enable brhodiumnode.service
