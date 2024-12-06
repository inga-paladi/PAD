#!/bin/bash

echo "The init steps are in readme.md"
exit 0

present_volumes=$(docker volume ls -q | grep -E "^(posts-db|comments-db-[123])$")

[ "$present_volumes" ] \
    && echo "Run \"docker volume remove posts-db comments-db-1 comments-db-2 comments-db-3\" before runnning this script" \
    && exit 0

for volume in posts-db comments-db-1 comments-db-2 comments-db-3; do
    docker volume create $volume
done

echo -e "Docker compose will run and apply migrations.\n\
Press CTRL + C when you see that comments-migrator \n\
and posts-migrator exited successfully"
read -n1 -s -p "Press any key to continue..."

docker compose -f docker-compose.db.init.yaml up --build