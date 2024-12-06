#!/bin/bash

# mysql --host=${DATABASE_HOST} --user=root --password=changeme -e "SET GLOBAL group_replication_bootstrap_group=ON; START GROUP_REPLICATION; SET GLOBAL group_replication_bootstrap_group=OFF;"

# sleep 10

mysqlsh root:changeme@${DATABASE_HOST}:3306 --execute "var cluster = dba.createCluster('prodCluster', {adoptFromGR: true});"