create table outbox.outbox_offsets
(
    "offset" bigint not null
);

INSERT INTO outbox.outbox_offsets ("offset") VALUES (0);

create table outbox.outbox_events
(
    id           bigint generated always as identity
        constraint outbox_events_pk
            primary key,
    key          varchar(200),
    value        text                                           not null,
    topic        varchar(1000)                                  not null,
    created_by   varchar(200),
    created_date timestamp default (now() AT TIME ZONE 'UTC') not null,
    header       text
);

create table outbox.missing_outbox_events
(
    id               bigint                not null,
    missed_date      timestamp             not null,
    retry_count      integer default 0     not null,
    exception_thrown boolean default false not null
);

create table outbox.exceeded_outbox_events
(
    id               bigint                not null,
    missed_date      timestamp             not null,
    retry_count      integer default 0     not null,
    exceeded_date    timestamp             not null,
    exception_thrown boolean default false not null
);