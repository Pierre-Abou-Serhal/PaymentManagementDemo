/* PaymentManagementDemo — SQL Server 2016. Apply before 002_data.sql. */
IF DB_ID(N'PaymentManagementDemo') IS NULL CREATE DATABASE PaymentManagementDemo;
GO
USE PaymentManagementDemo;
GO
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF OBJECT_ID(N'dbo.t_institutions', N'U') IS NULL
CREATE TABLE dbo.t_institutions (
 id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
 code nvarchar(32) NOT NULL UNIQUE,
 display_name nvarchar(128) NOT NULL,
 is_active bit NOT NULL CONSTRAINT df_institution_active DEFAULT(1),
 created_utc datetime2(3) NOT NULL CONSTRAINT df_institution_created DEFAULT(SYSUTCDATETIME()),
 rv rowversion NOT NULL
);
GO
IF OBJECT_ID(N'dbo.t_iso_definitions', N'U') IS NULL
CREATE TABLE dbo.t_iso_definitions (
 code varchar(32) NOT NULL PRIMARY KEY,
 family varchar(8) NOT NULL CHECK (family IN ('pain.001','pain.002')),
 namespace_uri nvarchar(200) NOT NULL,
 schema_file nvarchar(260) NULL,
 schema_sha256 binary(32) NULL,
 source_url nvarchar(500) NULL,
 is_tested bit NOT NULL CONSTRAINT df_iso_tested DEFAULT(0)
);
GO
IF OBJECT_ID(N'dbo.t_response_mappings', N'U') IS NULL
CREATE TABLE dbo.t_response_mappings (
 institution_id int NOT NULL REFERENCES dbo.t_institutions(id),
 input_code varchar(32) NOT NULL REFERENCES dbo.t_iso_definitions(code),
 output_code varchar(32) NOT NULL REFERENCES dbo.t_iso_definitions(code),
 revision int NOT NULL CONSTRAINT df_mapping_revision DEFAULT(1),
 rv rowversion NOT NULL,
 CONSTRAINT pk_response_mappings PRIMARY KEY (institution_id,input_code)
);
GO
IF OBJECT_ID(N'dbo.t_demo_routing_rules', N'U') IS NULL
CREATE TABLE dbo.t_demo_routing_rules (
 currency char(3) NOT NULL PRIMARY KEY,
 threshold decimal(28,8) NOT NULL CHECK (threshold > 0),
 revision int NOT NULL CONSTRAINT df_rule_revision DEFAULT(1),
 rv rowversion NOT NULL
);
GO
IF OBJECT_ID(N'dbo.t_file_receipts', N'U') IS NULL
CREATE TABLE dbo.t_file_receipts (
 id uniqueidentifier NOT NULL PRIMARY KEY,
 intake_institution_id int NOT NULL REFERENCES dbo.t_institutions(id),
 effective_institution_id int NOT NULL REFERENCES dbo.t_institutions(id),
 channel varchar(12) NOT NULL CHECK (channel IN ('Poll','Upload')),
 original_filename nvarchar(260) NOT NULL,
 original_path nvarchar(1000) NULL,
 current_path nvarchar(1000) NULL,
 received_utc datetime2(3) NOT NULL,
 source_bytes varbinary(max) NOT NULL,
 byte_length bigint NOT NULL,
 checksum binary(32) NOT NULL,
 canonical_checksum binary(32) NULL,
 import_state varchar(20) NOT NULL CHECK (import_state IN ('Received','Claimed','Validating','Imported','ImportFailed','Duplicate')),
 location_state varchar(20) NOT NULL CHECK (location_state IN ('Inbound','Claimed','ArchivePending','Archived','ErrorMovePending','Error')),
 detected_definition varchar(32) NULL,
 error_type varchar(64) NULL,
 validation_json nvarchar(max) NULL,
 duplicate_of uniqueidentifier NULL REFERENCES dbo.t_file_receipts(id),
 rv rowversion NOT NULL,
 CONSTRAINT ck_receipt_canonical CHECK ((canonical_checksum IS NULL AND import_state IN ('Received','Claimed','Validating','Duplicate')) OR (canonical_checksum IS NOT NULL AND import_state IN ('Imported','ImportFailed')))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.t_file_receipts') AND name=N'ux_receipt_canonical')
CREATE UNIQUE INDEX ux_receipt_canonical ON dbo.t_file_receipts(effective_institution_id,canonical_checksum) WHERE canonical_checksum IS NOT NULL;
GO
IF OBJECT_ID(N'dbo.t_payment_batches', N'U') IS NULL
CREATE TABLE dbo.t_payment_batches (
 id uniqueidentifier NOT NULL PRIMARY KEY,
 receipt_id uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.t_file_receipts(id),
 effective_institution_id int NOT NULL REFERENCES dbo.t_institutions(id),
 input_code varchar(32) NOT NULL REFERENCES dbo.t_iso_definitions(code),
 output_code varchar(32) NULL REFERENCES dbo.t_iso_definitions(code),
 mapping_revision int NULL,
 original_message_id nvarchar(100) NULL,
 original_creation nvarchar(64) NULL,
 state varchar(16) NOT NULL CHECK (state IN ('Editable','Finalizing','Finalized')),
 current_revision int NOT NULL CONSTRAINT df_batch_revision DEFAULT(0),
 assignment_locked_utc datetime2(3) NULL,
 created_utc datetime2(3) NOT NULL,
 rv rowversion NOT NULL
);
GO
IF OBJECT_ID(N'dbo.t_payment_groups', N'U') IS NULL
CREATE TABLE dbo.t_payment_groups (
 id uniqueidentifier NOT NULL PRIMARY KEY,
 batch_id uniqueidentifier NOT NULL REFERENCES dbo.t_payment_batches(id),
 source_ordinal int NOT NULL,
 payment_info_id nvarchar(100) NULL,
 debtor_name nvarchar(500) NULL,
 debtor_account_id nvarchar(200) NULL,
 debtor_account_kind varchar(20) NULL,
 debtor_account_currency char(3) NULL,
 debtor_agent nvarchar(200) NULL,
 requested_execution_date nvarchar(64) NULL,
 source_xml nvarchar(max) NULL,
 CONSTRAINT ux_group_ordinal UNIQUE(batch_id,source_ordinal)
);
GO
IF OBJECT_ID(N'dbo.t_transfers', N'U') IS NULL
CREATE TABLE dbo.t_transfers (
 id uniqueidentifier NOT NULL PRIMARY KEY,
 batch_id uniqueidentifier NOT NULL REFERENCES dbo.t_payment_batches(id),
 group_id uniqueidentifier NOT NULL REFERENCES dbo.t_payment_groups(id),
 source_ordinal int NOT NULL,
 instruction_id nvarchar(100) NULL,
 end_to_end_id nvarchar(100) NULL,
 creditor_name nvarchar(500) NULL,
 creditor_account_id nvarchar(200) NULL,
 creditor_account_kind varchar(20) NULL,
 creditor_agent nvarchar(200) NULL,
 instructed_amount decimal(28,8) NULL,
 instructed_currency char(3) NULL,
 equivalent_amount decimal(28,8) NULL,
 equivalent_currency char(3) NULL,
 transfer_currency char(3) NULL,
 account_currency char(3) NULL,
 routing_amount decimal(28,8) NULL,
 iban_country char(2) NULL,
 iban_bank_code char(4) NULL,
 rule_revision int NULL,
 routing_code varchar(64) NULL,
 routing_explanation nvarchar(1000) NULL,
 category varchar(8) NOT NULL CHECK (category IN ('LOCAL','ACH','RTGS','SWIFT','INVALID')),
 decision varchar(8) NOT NULL CHECK (decision IN ('Pending','Approved','Rejected')),
 decision_source varchar(8) NULL CHECK (decision_source IN ('System','Operator')),
 decision_reason nvarchar(2000) NULL,
 decision_actor nvarchar(200) NULL,
 decision_utc datetime2(3) NULL,
 source_xml nvarchar(max) NOT NULL,
 rv rowversion NOT NULL,
 CONSTRAINT ux_transfer_ordinal UNIQUE(group_id,source_ordinal),
 CONSTRAINT ck_invalid_decision CHECK (category <> 'INVALID' OR (decision='Rejected' AND decision_source='System' AND decision_reason IS NOT NULL))
);
GO
IF OBJECT_ID(N'dbo.t_batch_categories', N'U') IS NULL
CREATE TABLE dbo.t_batch_categories (
 batch_id uniqueidentifier NOT NULL REFERENCES dbo.t_payment_batches(id),
 category varchar(8) NOT NULL CHECK (category IN ('LOCAL','ACH','RTGS','SWIFT','INVALID')),
 processed_utc datetime2(3) NULL,
 processed_actor nvarchar(200) NULL,
 CONSTRAINT pk_batch_categories PRIMARY KEY(batch_id,category),
 CONSTRAINT ck_invalid_unprocessed CHECK (category <> 'INVALID' OR processed_utc IS NULL)
);
GO
IF OBJECT_ID(N'dbo.t_outgoing_documents', N'U') IS NULL
CREATE TABLE dbo.t_outgoing_documents (
 id uniqueidentifier NOT NULL PRIMARY KEY,
 kind varchar(16) NOT NULL CHECK (kind IN ('Pain002','ImportErrorJson')),
 receipt_id uniqueidentifier NOT NULL REFERENCES dbo.t_file_receipts(id),
 batch_id uniqueidentifier NULL REFERENCES dbo.t_payment_batches(id),
 revision int NULL,
 operation_id uniqueidentifier NULL,
 recipient_institution_id int NOT NULL REFERENCES dbo.t_institutions(id),
 iso_definition varchar(32) NULL REFERENCES dbo.t_iso_definitions(code),
 message_id nvarchar(100) NULL,
 filename nvarchar(260) NOT NULL,
 mime_type varchar(80) NOT NULL,
 content_bytes varbinary(max) NOT NULL,
 content_sha256 binary(32) NOT NULL,
 generated_utc datetime2(3) NOT NULL,
 publication_state varchar(24) NOT NULL CHECK (publication_state IN ('Pending','Publishing','PublishedToOut','Failed','SupersededUnpublished')),
 publication_started_utc datetime2(3) NULL,
 next_attempt_utc datetime2(3) NULL,
 published_utc datetime2(3) NULL,
 retry_count int NOT NULL CONSTRAINT df_doc_retry DEFAULT(0),
 last_error nvarchar(2000) NULL,
 rv rowversion NOT NULL,
 CONSTRAINT ck_doc_shape CHECK ((kind='Pain002' AND batch_id IS NOT NULL AND revision IS NOT NULL AND iso_definition IS NOT NULL AND message_id IS NOT NULL) OR (kind='ImportErrorJson' AND batch_id IS NULL AND revision IS NULL))
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.t_outgoing_documents') AND name=N'ux_doc_revision')
CREATE UNIQUE INDEX ux_doc_revision ON dbo.t_outgoing_documents(batch_id,revision) WHERE kind='Pain002';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.t_outgoing_documents') AND name=N'ux_doc_error_receipt')
CREATE UNIQUE INDEX ux_doc_error_receipt ON dbo.t_outgoing_documents(receipt_id) WHERE kind='ImportErrorJson';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.t_outgoing_documents') AND name=N'ux_doc_operation')
CREATE UNIQUE INDEX ux_doc_operation ON dbo.t_outgoing_documents(operation_id) WHERE operation_id IS NOT NULL;
GO
IF OBJECT_ID(N'dbo.t_workflow_audit', N'U') IS NULL
CREATE TABLE dbo.t_workflow_audit (
 id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
 occurred_utc datetime2(3) NOT NULL CONSTRAINT df_audit_time DEFAULT(SYSUTCDATETIME()),
 actor nvarchar(200) NOT NULL,
 action varchar(64) NOT NULL,
 receipt_id uniqueidentifier NULL REFERENCES dbo.t_file_receipts(id),
 batch_id uniqueidentifier NULL REFERENCES dbo.t_payment_batches(id),
 transfer_id uniqueidentifier NULL REFERENCES dbo.t_transfers(id),
 document_id uniqueidentifier NULL REFERENCES dbo.t_outgoing_documents(id),
 details_json nvarchar(max) NULL
);
GO
