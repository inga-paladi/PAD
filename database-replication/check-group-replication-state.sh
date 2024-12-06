PASSWORD=changeme

for N in 1 2 3
do docker exec -i comments-database-$N mysql -uroot -p${PASSWORD} \
  -e "SHOW VARIABLES WHERE Variable_name = 'hostname';" \
  -e "SELECT * FROM performance_schema.replication_group_members;"
done