CREATE DATABASE warehouse;

USE warehouse;

CREATE TABLE comments (
    Id CHAR(36),
    PostGuid CHAR(36),
    ReplyGuid CHAR(36),
    Content LONGTEXT,
    CreationTime DATETIME(6),
    LastEditTime DATETIME(6),

    insert_time DATETIME(6)
);

CREATE TABLE posts (
    Id CHAR(36),
    Title LONGTEXT,
    Content LONGTEXT,
    CreationTime DATETIME(6),
    LastEditTime DATETIME(6),

    insert_time DATETIME(6)
);