#  Lab 2 - Logically Linked DBs
## Checkpoint 1
### Topic: MeoWorld: A Simple Cat-Themed Blogging Platform

The MeoWorld platform leverages the popularity of cat-related content online. This blogging platform attracts a wide audience by allowing users to interact with posts through likes and comments, creating a community for cat lovers.

---

# Project Overview

### Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Components](#components)
   - [1. Circuit Breaker with Custom Rerouting Logic](#circuit-breaker-with-custom-rerouting-logic)
   - [2. Service High Availability](#service-high-availability)
   - [3. Logging with ELK Stack](#logging-with-elk-stack)
   - [4. Consistent Hashing for Cache](#consistent-hashing-for-cache)
   - [5. Long-Running Saga Transactions](#long-running-saga-transactions)
   - [6. Database Redundancy and Replication](#database-redundancy-and-replication)
   - [7. Data Warehouse Integration](#data-warehouse-integration)
3. [Technologies Used](#technologies-used)
4. [Setup and Configuration](#setup-and-configuration)
5. [Running the System](#running-the-system)
6. [Troubleshooting and Maintenance](#troubleshooting-and-maintenance)

---

## Architecture Overview

This project implements a resilient microservices architecture designed for high availability, load balancing, and fault tolerance. Services are managed with a custom load balancer that incorporates health checks, circuit breaking, and dynamic rerouting. A distributed cache, saga transaction coordinator, and database replication ensure consistency and reliability across services.
![Architecture](architecture.png)

---

## Components

### 1. Circuit Breaker with Custom Rerouting Logic
The trigger depends on reroutes themselves, not errors. Even if a service instance responds within acceptable time limits, repeated rerouting implies instability (like random disconnections or intermittent availability). The rerouting threshold (e.g., three reroutes in 3.5 * timeout) would act as a signal to “trip” the circuit, temporarily blocking that instance.

### 2. Service High Availability
The custom load balancer can ensure high availability by:
- Running instances across multiple geographic locations.
- Using caching for service registry information, which would help it quickly reestablish service paths after failures without waiting for new health checks.
- Supporting failover to healthy instances and removing any that exceed rerouting or error thresholds.

For failover, any single point of failure (e.g., the load balancer itself) should be backed up by quick-recovery mechanisms or redundancy, ensuring uninterrupted service to clients.
### 3. Logging with ELK Stack
ELK (Elasticsearch, Logstash, Kibana) is ideal for logging in distributed microservices architectures. Each component has a separate role:
- Logstash collects logs from each service and processes them.
- Elasticsearch stores the logs in an efficient, queryable format.
- Kibana enables visualization for real-time monitoring.

Each service should send logs to Logstash, and Logstash should have a way to send logs to Elasticsearch, so Kibana can pull them for monitoring. Docker containers for each component keep the logging architecture modular.

### 4. Consistent Hashing for Cache
- **Hash Ring as Load Balancer**: A central service maintains a hash ring that maps GUIDs (e.g., post IDs) to Redis cache servers.

- **Redis Servers**: Deploy two Redis servers for caching, each managing a portion of the data based on the hash ring.

- **Cache Lookups**: When a request for a post is made, the hash ring service hashes the GUID, determines the appropriate Redis server, and forwards the request.

- **Centralized Management**: The hash ring service ensures even distribution and rebalances servers as needed, improving cache availability and efficiency.

### 5. Long-Running Saga Transactions
- Saga Coordinator: This is responsible for orchestrating transactional flows between microservices. By isolating this responsibility to a dedicated service, other services won’t need to manage dependencies or be aware of other services. Instead, the Saga Coordinator handles this for them.

- Transaction Identification: Adding a header like X-saga-transaction: true allows services to identify transactions as part of a saga. For instance, if a request includes this header, the endpoint (/v1/blog/posts) would either process as usual or return a transaction ID if it’s part of a saga. This transaction ID could then be used to roll back to the initial state if needed.

- Transaction Storage: Each transaction, along with its compensating actions, should be saved in case a rollback is necessary. Any instance of the service should be able to access these transactions to enable high availability.

- Load Balancer Integration: The Saga Coordinator will interact with a custom load balancer, planned as a separate service. This load balancer will help the coordinator communicate with available service instances without directly handling databases, cache, or logs.

### 6. Database Redundancy and Replication
- **Database Configuration**: The architecture will include instances of database services set up with redundancy. One database instance will act as the primary (master), while another will serve as a secondary (slave) database.
- **High Availability**: All service instances will access these databases to ensure continuous operations. In case of master database failure, the slave will take over seamlessly.


### 7. Data Warehouse Integration
- **Warehouse Setup**: The data warehouse will consolidate information from the slave databases to minimize the load on the master database.

- **ETL Process**: Using Apache NiFi for the ETL (Extraction, Transformation, Loading) process, the workflow will:

    - Extract data from the slave databases.
    - Transform the data into the required format.
    - Load it into the data warehouse, which will utilize Apache Hive.
- **Periodic Synchronization**: NiFi will periodically check for updates in the slave databases by monitoring modification timestamps, extracting new and updated records (e.g., posts and comments), and loading them into the data warehouse.




## Technologies Used

- **Programming Languages**:  
   - **C#** - Main backend microservices for content and user management.
   - **Go** - Optimized microservices for high-throughput tasks like comment processing and image handling.
  
- **Service Architecture**:
   - **ASP.NET Core** - Core framework for the backend APIs.
   - **gRPC** - Lightweight communication between microservices.
   - **Redis** - Used for queue-based tasks, like managing likes, comments, and notifications.

- **Database Systems**:
   - **MySQL** 
   - **Redis** - Distributed cache to store popular blog post data and likes, offloading frequent reads from the primary database.

- **Monitoring & Analytics**:
   - **ELK Stack** - Logs managed with Elasticsearch, Logstash, and visualized with Kibana for complete system monitoring.


- **Load Balancer**:  
   - **Custom Load Balancer** - Created with fallback logic, rerouting strategies, and circuit breaker integration.
  
- **Data Management**:
   - **Apache Hive** - Data warehousing for analytics, with nightly ETL via NiFi 

---
## Data Management (Database + Endpoints)
Below, in the proto files, the services, their endpoints, and respective requests, responses, and data structures are defined. The service will provide endpoints for managing posts in a CRUD style. Additionally, the WebSocket implementation will follow this structure:

- The client connects to the gateway using the WebSocket protocol.
- The gateway then utilizes the streaming feature in proto/gRPC to receive unidirectional messages from the services.

```proto
syntax = "proto3";
package meoworld;

service Blog {
  rpc PublishPost( PublishPostRequest ) returns ( PublishPostResponse ) {
    option (google.api.http) = {
      post: "/v1/blog/post"
      body: "*"
    };
  }
  rpc EditPost( EditPostRequest ) returns ( stream EditPostRequest ) {
    option (google.api.http) = {
      post: "/v1/blog/post/{guid}/edit"
      body: "*"
    };
  }
  rpc DeletePost( DeletePostRequest ) returns ( stream DeletePostRequest ) {
    option (google.api.http) = {
      post: "/v1/blog/post/{guid}/delete"
      body: "*"
    };
  }
  rpc ListPosts( ListPostsRequest ) returns ( ListPostsResponse ) {
    option (google.api.http) = {
      get: "/v1/blog/post"
    };
  }
  rpc GetPost( GetPostRequest ) returns ( GetPostResponse ) {
    option (google.api.http) = {
      get: "/v1/blog/post/{guid}"
    };
  }
  rpc Listen( ListenRequest ) returns ( stream ListenRequest ) { }
}

message PublishPostRequest {
  string title = 1;
  string content = 2;
}
message PublishPostResponse {
  string guid = 1;
}

message EditPostRequest {
  string guid = 1;
  string title = 2;
  string content = 3;
}
message EditPostResponse {
  // Empty
}

message DeletePostRequest {
  string guid = 1;
}
message DeletePostResponse {
  // Empty
}

message ListPostsRequest {
  // filters
  uint8 limit = 1;
  google.protobuf.Timestamp after_time = 2;
  google.protobuf.Timestamp before_time = 3;
}
message ListPostsResponse {
  repeated BlogPost posts = 1;
}

message GetPostRequest {
  string guid = 1;
}
message GetPostResponse {
  BlogPost post = 1;
}

message ListenRequest {
  repeated ListenType types = 1;
}
message ListenRequest {
  ListenType type = 1;
  // The data will be parsed to a special message, based on message type.
  byte data = 2;
}

// Types (should be separate file, but keep it here for simplicity)
message BlogPost {
  string guid = 1;
  uint64 owner_id = 2;
  string title = 3;
  string content = 4;
  google.protobuf.Timestamp creation_time = 5;
  google.protobug.Timestamp = last_edited_time = 6;
}

enum ListenType {
  LISTEN_TYPE_UNSPECIFIED,
  LISTEN_TYPE_NEW_POST,
}

message NewPostNotification {
  string guid = 1;
}
```

The "comments" service will handle the management of blog post 
comments, offering endpoints for CRUD operations.
```proto
syntax = "proto3";
package meoworld;

service Comments {
  rpc AddComment( AddCommentRequest ) returns ( AddCommentResponse ) {
    option (google.api.http) = {
      post: "/v1/comment"
      body: "*"
    };
  }
  rpc ListComments( ListCommentsRequest ) returns ( ListCommentsResponse ) {
    option (google.api.http) = {
      get: "/v1/comment/{post_guid}"
    };
  }
  rpc EditComment( EditCommentRequest ) returns ( EditCommentResponse ) {
    option (google.api.http) = {
      post: "/v1/comment/{guid}/edit"
      body: "*"
    };
  }
  rpc DeleteComment( DeleteCommentRequest ) returns ( DeleteCommentResponse ) {
    option (google.api.http) = {
      post: "/v1/comment/{guid}/delete"
      body: "*"
    };
  }
}

message AddCommentRequest {
  string post_guid = 1
  string content = 2;
  string reply_guid = 3;
}
message AddCommentResponse {
  string guid = 1;
}

message ListCommentsRequest {
  string post_guid = 1;
}
message ListCommentsResponse {
  repeated Comment comments = 1;
}

message EditCommentRequest {
  string guid = 1
  string content = 2;
}
message EditCommentResponse {
  // Empty
}

message DeleteCommentRequest {
  string guid = 1
}
message DeleteCommentResponse {
  // Empty
}

// Types
message Comment {
  string guid = 1;
  string post_guid = 2;
  // The comment guid to which it responds
  string reply_guid = 3;
  uint64 owner_id = 4;
  string content = 5;
  google.protobug.Timestamp creation_time = 6;
  google.protobug.Timestamp last_edited_time = 7;
}
```
---

## Running/Test/Deploy the System

1. **Running Services**: 
To start all services, use
```
docker compose up
```
2. **Stopping Services**: To stop all services, use:
```
docker compose down
```

3. **Initializing Volumes**: Before running with Docker Compose, you may want to initialize the volume for each service. To do this, run the init script located in each service directory:
`init-posts-db-volume.sh` and `init-comments-db-volume.sh`

---
4. **Docker Compose Configuration example**:  
```
services:
  redis:
    image: redis:latest
    hostname: meoworld-redis

  gateway:
    build:
      context: ./
      dockerfile: GatewayDockerfile
    ports:
      - "8080:8080"
    environment:
      - REDIS_ADDRESS=meoworld-redis
    depends_on:
      - redis

  posts-service:
    build:
      context: ./
      dockerfile: ./services/posts/Dockerfile
    environment:
      - REDIS_ADDRESS=meoworld-redis
      - DB_PATH=/database/
    volumes:
      - posts-db:/database
    deploy:
      mode: replicated
      replicas: 3
    depends_on:
      - redis
      - gateway

volumes:
  posts-db:
    external: true
  comments-db:
    external: true
```