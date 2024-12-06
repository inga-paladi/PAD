#!/bin/bash

docker exec -i comments-database-1 mysql --user=root --password=changeme -e "SET GLOBAL group_replication_bootstrap_group=ON; START GROUP_REPLICATION; SET GLOBAL group_replication_bootstrap_group=OFF;"

# docker exec -i comments-database-1 mysql --user=root --password=changeme -e "START GROUP_REPLICATION;"

docker exec -i comments-database-2 mysql --user=root --password=changeme -e "START GROUP_REPLICATION;"
docker exec -i comments-database-3 mysql --user=root --password=changeme -e "START GROUP_REPLICATION;"

# function wait-db-ready()
# {
#     until docker exec -i $1 mysql --user=root --password=changeme -e "SELECT 1" 2> /dev/null; do
#         sleep 1
#     done
# }

# this is for the mysql router to work with bootstrap
# should be run from mysqlsh
# https://forums.mysql.com/read.php?146,677277,677321
# var cluster = dba.createCluster('prodCluster', {adoptFromGR: true});


# ./check-group-replication-state.sh