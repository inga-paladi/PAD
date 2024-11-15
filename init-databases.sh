#!/bin/bash

function check_volume() {
    VOLUME_NAME=$1
    [ -n "$(docker volume ls -q --filter "name=$VOLUME_NAME")" ] \
        && echo "Docker volume '$VOLUME_NAME' already exists" \
        && echo -e "To remove it, run \"docker volume remove $VOLUME_NAME\" and rerun the script" \
        && exit 0
}

check_volume posts-db
check_volume comments-db

docker volume create posts-db
docker volume create comments-db

echo -e "Docker compose will run and apply migrations.\n\
Press CTRL + C when you see that comments-migrator \n\
and posts-migrator exited successfully"
read -n1 -p "Press any key to continue..."

docker compose -f docker-compose.db.init.yaml up --build