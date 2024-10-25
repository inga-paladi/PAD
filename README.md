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
For generating a new volume and populate it with the sqlite file, run the init script located in each service dir:
`init-posts-db-volume.sh` and `init-comments-db-volume.sh`

Important resources:
https://grpc-ecosystem.github.io/grpc-gateway/
https://pkg.go.dev/google.golang.org/grpc
https://github.com/grpc-ecosystem/grpc-gateway/tree/main/examples/internal
https://www.stevejgordon.co.uk/health-checks-with-grpc-and-asp-net-core-3
https://github.com/grpc/grpc/blob/master/doc/service_config.md
https://github.com/grpc/grpc-go/tree/master/examples/features
