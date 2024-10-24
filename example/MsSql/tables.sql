
create table ExceededOutboxEvents
(
    Id              bigint        not null,
    MissedDate      datetime2     not null,
    RetryCount      int default 0 not null,
    ExceededDate    datetime2     not null,
    ExceptionThrown bit           not null
)
go

create table MissingOutboxEvents
(
    Id              bigint        not null,
    MissedDate      datetime2     not null,
    RetryCount      int default 0 not null,
    ExceptionThrown bit default 0 not null
)
go

create table OutboxEvents
(
    Id          bigint identity
        constraint PK_OutboxEvents
            primary key,
    [Key]       nvarchar(200),
    Value       ntext          not null,
    Topic       nvarchar(1000) not null,
    CreatedBy   nvarchar(200),
    CreatedDate datetime2(3),
    Header      nvarchar(max)
)
go

create index NCIX_CreatedDate
    on OutboxEvents (CreatedDate)
    with (fillfactor = 90)
go

create table OutboxOffsets
(
    Offset bigint not null
)
go