#!/bin/bash

VOLUME_NAME="comments-db"

[ -n "$(docker volume ls -q --filter "name=$VOLUME_NAME")" ] \
    && echo "Docker volume '$VOLUME_NAME' already exists" \
    && echo -e "To remove it, run \"docker volume remove $VOLUME_NAME\" and rerun the script" \
    && exit 0

# create image that builds and migrates the db file.
docker buildx build --file Dockerfile.init --tag ${VOLUME_NAME}-init-image ../../ || exit 1

docker volume create $VOLUME_NAME
docker run --rm --volume $VOLUME_NAME:/database ${VOLUME_NAME}-init-image

echo "Volume created and populated"