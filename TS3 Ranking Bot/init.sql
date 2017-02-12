create table if not exists {{prefix}}users (
    id int(11) not null auto_increment primary key,
    uuid varchar(30) not null unique,
    cur_level int(11) not null,
    cur_time int(11) not null
);