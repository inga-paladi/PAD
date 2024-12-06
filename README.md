[Tutorial](https://grpc-ecosystem.github.io/grpc-gateway/docs/tutorials/introduction/)

[buf](https://buf.build/docs/installation) should be installed.
<br>
[buf plugins](https://buf.build/plugins).

Steps to generate proto files:
```bash
$ buf dep update
$ buf generate
```

run all services from one command
`docker compose up`
to stop them
`docker compose down`

Before running using docker compose, you may want to init the volume for each service.

Astea de jos functioneaza pana la partea de router, el nu gaseste comments 1 ca fiind sin grup
clusterul se face cand doar db1 e pornit, nu trebuie sa fie incluse si celelalte doua, ca dupa asta sa nu fie probleme.
## 1. DB Initialization
1. Open `docker-compose-databases.yaml`;
2. Make sure the lines with `loose-group-replication-start-on-boot=OFF` are NOT commented;
3. Run `docker compose -f docker-compose-databases.yaml up comments-database-1 comments-database-2 comments-database-3 posts-database` and wait until they finish init process;
4. Run `docker compose -f docker-compose-db-init.yaml up --build comments-migrator posts-migrator`;
5. Use CTRL + C to stop the initial `docker-compose-databases` project;
6. Comment the lines in step 2;
7. Run `docker compose -f docker-compose-databases.yaml up comments-database-1` wait until is ready (listening);
8. Run `docker compose -f docker-compose-db-init.yaml up comments-db-cluster-creator`;
9. Repeat step 4 to close the container.
pana aici fac arhiva

## DB Init attempt 2
Make sure to run `docker compose -f docker-compose-databases.yaml down --volumes` before.

1. Run `docker compose -f docker-compose-databases.yaml up comments-database-1 comments-database-2 comments-database-3 posts-database` and wait until they finish init process;
2. Run `docker compose -f docker-compose-db-init.yaml up --build comments-migrator posts-migrator`;
3. Run `./database-replication/initiate-replication.sh`
<!-- 3. Run `docker exec -it comments-database-1 mysql -uroot -pchangeme -e "SET GLOBAL group_replication_bootstrap_group=ON; START GROUP_REPLICATION; SET GLOBAL group_replication_bootstrap_group=OFF;"` -->
4. Run `docker exec -it comments-database-1 mysqlsh root:changeme@comments-database-1:3306 --execute "var cluster = dba.createCluster('prodCluster', {adoptFromGR: true});"`
5. Run `docker compose -f docker-compose-databases.yaml up mysql-router`;

Now, don't stopy any. I didn't find a way to restore them back.
If you the master. Another one will take its place. If you start it, make sure to start as
usual, with bootstrap ON. But still, it is not fully online, is in a Recovering state.
<!-- 6. Use CTRL + C to stop the initial `docker-compose-databases` project; -->


Important resources:
https://grpc-ecosystem.github.io/grpc-gateway/
https://pkg.go.dev/google.golang.org/grpc
https://github.com/grpc-ecosystem/grpc-gateway/tree/main/examples/internal
https://www.stevejgordon.co.uk/health-checks-with-grpc-and-asp-net-core-3
https://github.com/grpc/grpc/blob/master/doc/service_config.md
https://github.com/grpc/grpc-go/tree/master/examples/features

## 2. ELK Stack

https://www.elastic.co/guide/en/kibana/current/docker.html#run-kibana-on-docker-for-dev
https://www.elastic.co/blog/getting-started-with-the-elastic-stack-and-docker-compose
https://pmihaylov.com/go-service-with-elk/

Golang Logging
https://betterstack.com/community/guides/logging/go/zap/
