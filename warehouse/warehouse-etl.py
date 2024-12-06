from sqlalchemy import create_engine
import datetime
import time
import os

# INSERT INTO Comments(Id, OwnerId, CreationTime, LastEditedTime, PostGuid, Content) VALUES ("4502a9f3-bc2b-432d-a7c0-16948f201a9f", 1, "2024-12-02 18:42:46.357342", "2024-12-02 18:42:46.357342", "cd8adf63-b4c3-493e-b82c-620ff8cb4835", "Content1");
# INSERT INTO Comments(Id, OwnerId, CreationTime, LastEditedTime, PostGuid, Content) VALUES ("1d7435ea-2cdb-4017-bc73-a0a33ea6af50", 1, "2024-12-02 18:42:46.357342", "2024-12-02 18:42:46.357342", "cd8adf63-b4c3-493e-b82c-620ff8cb4835", "Content2");
# INSERT INTO Posts(Id, OwnerId, Title, Content, CreationTime, LastEditedTime) VALUES("cd8adf63-b4c3-493e-b82c-620ff8cb4835", 1, "Post title", "The post content", "2024-11-14 20:58:58.406822", "2024-11-14 20:58:58.406822")

def get_warehouse_connection():
    warehouse_address = os.environ.get('WAREHOUSE_ADDRESS')
    warehouse_port = os.environ.get('WAREHOUSE_PORT')
    if not warehouse_address:
        warehouse_address = "localhost"
    if not warehouse_port:
        warehouse_port = 33306
    warehouse_conn_string = f"mysql+pymysql://root:changeme@{warehouse_address}:{warehouse_port}/warehouse"
    return create_engine(warehouse_conn_string).connect()

def get_comments_connection():
    comments_db_address = os.environ.get("COMMENTS_DATABASE_ADDRESS")
    comments_db_port = os.environ.get("COMMENTS_DATABASE_PORT")
    if not comments_db_address:
        comments_db_address = "localhost"
    if not comments_db_port:
        comments_db_port = 6446
    comments_conn_string = f"mysql+pymysql://root:changeme@{comments_db_address}:{comments_db_port}/comments"
    return create_engine(comments_conn_string).connect()

def get_posts_connection():
    posts_db_address = os.environ.get("POSTS_DATABASE_ADDRESS")
    posts_db_port = os.environ.get("POSTS_DATABASE_PORT")
    if not posts_db_address:
        posts_db_address = "localhost"
    if not posts_db_port:
        posts_db_port = 3336
    posts_conn_string = f"mysql+pymysql://root:changeme@{posts_db_address}:{posts_db_port}/posts"
    return create_engine(posts_conn_string).connect()

warehouse_conn = get_warehouse_connection()
comments_conn = get_comments_connection()
posts_conn = get_posts_connection()

def process_last_comments():
    comments_selection_query = "SELECT * FROM Comments WHERE LastEditedTime > NOW() - INTERVAL 60 SECOND;"
    comments = comments_conn.execute(comments_selection_query).fetchall()
    print(f"{len(comments)} comments found", flush=True)

    for comment in comments:
        Id, PostGuid, ReplyGuid, OwnerId, Content, CreationTime, LastEditTime = comment

        insert_query = f'INSERT INTO comments(Id, PostGuid, Content, CreationTime, LastEditTime, insert_time) VALUES ("{Id}", "{PostGuid}", "{Content}", "{CreationTime}", "{LastEditTime}", "{str(datetime.datetime.now())}")'

        warehouse_conn.execute(insert_query)

def process_last_posts():
    posts_selection_query = "SELECT * FROM Posts WHERE LastEditedTime > NOW() - INTERVAL 60 SECOND;"
    posts = posts_conn.execute(posts_selection_query).fetchall()
    print(f"{len(posts)} posts found", flush=True)

    for post in posts:
        Id, _, Title, Content, CreationTime, LastEditTime = post

        insert_query = f'INSERT INTO posts(Id, Title, Content, CreationTime, LastEditTime, insert_time) VALUES ("{Id}", "{Title}", "{Content}", "{CreationTime}", "{LastEditTime}", "{str(datetime.datetime.now())}")'

        warehouse_conn.execute(insert_query)

try:
    while True:
        print("Processing new data", flush=True)
        process_last_comments()
        process_last_posts()
        time.sleep(50)
except KeyboardInterrupt:
    print("Exiting Warehouse ETL")