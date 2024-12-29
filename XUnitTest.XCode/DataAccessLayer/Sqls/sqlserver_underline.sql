Create Table [user](
	id int IDENTITY(1,1) Primary Key,
	name nvarchar(50) NOT NULL DEFAULT '',
	password nvarchar(200) NULL,
	display_name nvarchar(50) NULL,
	sex int NOT NULL DEFAULT 0,
	mail nvarchar(50) NULL,
	mail_verified bit NOT NULL DEFAULT 0,
	mobile nvarchar(50) NULL,
	mobile_verified bit NOT NULL DEFAULT 0,
	code nvarchar(50) NULL,
	area_id int NOT NULL DEFAULT 0,
	avatar nvarchar(200) NULL,
	role_id int NOT NULL DEFAULT 3,
	role_ids nvarchar(200) NULL,
	department_id int NOT NULL DEFAULT 0,
	[online] bit NOT NULL DEFAULT 0,
	enable bit NOT NULL DEFAULT 0,
	age int NOT NULL DEFAULT 0,
	birthday datetime NULL,
	logins int NOT NULL DEFAULT 0,
	last_login datetime NULL,
	last_login_ip nvarchar(50) NULL,
	register_time datetime NULL,
	register_ip nvarchar(50) NULL,
	online_time int NOT NULL DEFAULT 0,
	ex1 int NOT NULL DEFAULT 0,
	ex2 int NOT NULL DEFAULT 0,
	ex3 float NOT NULL DEFAULT 0,
	ex4 nvarchar(50) NULL,
	ex5 nvarchar(50) NULL,
	ex6 nvarchar(50) NULL,
	update_user nvarchar(50) NOT NULL DEFAULT '',
	update_user_id int NOT NULL DEFAULT 0,
	update_ip nvarchar(50) NULL,
	update_time datetime NOT NULL DEFAULT '0001-01-01',
	remark nvarchar(500) NULL
)