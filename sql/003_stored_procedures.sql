/* Payment Management Demo runtime SQL.
   Apply after 001_schema.sql (and 002_data.sql for a fresh installation).
   Safe to reapply; does not alter existing payment data or replace permissions.
   CREATE stubs + ALTER are compatible with SQL Server 2016 RTM. */
USE [PaymentManagementDemo];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF TYPE_ID(N'dbo.t_transfer_id_list') IS NULL
    EXEC(N'CREATE TYPE dbo.t_transfer_id_list AS TABLE (id uniqueidentifier NOT NULL PRIMARY KEY);');
GO

IF OBJECT_ID(N'dbo.sp_database_available', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_database_available AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_database_available
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 1;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_institutions', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_institutions AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_institutions
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id,code,display_name AS displayName,is_active AS isActive FROM dbo.t_institutions ORDER BY code;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_routing_rules', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_routing_rules AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_routing_rules
AS
BEGIN
    SET NOCOUNT ON;

    SELECT currency,CONVERT(varchar(50),threshold) AS threshold,revision,CONVERT(varchar(50),rv,1) AS version FROM dbo.t_demo_routing_rules ORDER BY currency;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_response_mappings', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_response_mappings AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_response_mappings
    @institutionId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT input_code AS inputCode,output_code AS outputCode,revision FROM dbo.t_response_mappings WHERE institution_id=@institutionId ORDER BY input_code;
END;
GO

IF OBJECT_ID(N'dbo.sp_upsert_response_mapping', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_upsert_response_mapping AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_upsert_response_mapping
    @institutionId int,
    @input varchar(32),
    @output varchar(32)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS(SELECT 1 FROM dbo.t_response_mappings WITH(UPDLOCK,HOLDLOCK) WHERE institution_id=@institutionId AND input_code=@input)
     UPDATE dbo.t_response_mappings SET output_code=@output,revision=revision+1 WHERE institution_id=@institutionId AND input_code=@input
     ELSE INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@institutionId,@input,@output);
END;
GO

IF OBJECT_ID(N'dbo.sp_upsert_routing_rule', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_upsert_routing_rule AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_upsert_routing_rule
    @currency char(3),
    @threshold decimal(28,8)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS(SELECT 1 FROM dbo.t_demo_routing_rules WITH(UPDLOCK,HOLDLOCK) WHERE currency=@currency)
     UPDATE dbo.t_demo_routing_rules SET threshold=@threshold,revision=revision+1 WHERE currency=@currency
     ELSE INSERT dbo.t_demo_routing_rules(currency,threshold) VALUES(@currency,@threshold);
END;
GO

IF OBJECT_ID(N'dbo.sp_list_active_institutions', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_active_institutions AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_active_institutions
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id,code FROM dbo.t_institutions WHERE is_active=1;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_receipt_move_state', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_receipt_move_state AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_receipt_move_state
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT import_state AS ImportState,location_state AS LocationState,intake_institution_id AS IntakeInstitutionId FROM dbo.t_file_receipts WHERE id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_active_institution', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_active_institution AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_active_institution
    @institutionId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id,code FROM dbo.t_institutions WHERE id=@institutionId AND is_active=1;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_routing_thresholds', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_routing_thresholds AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_routing_thresholds
AS
BEGIN
    SET NOCOUNT ON;

    SELECT currency,threshold,revision FROM dbo.t_demo_routing_rules;
END;
GO

IF OBJECT_ID(N'dbo.sp_find_canonical_receipt', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_find_canonical_receipt AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_find_canonical_receipt
    @institutionId int,
    @hash binary(32)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP(1) id FROM dbo.t_file_receipts WITH(UPDLOCK,HOLDLOCK)
     WHERE effective_institution_id=@institutionId AND canonical_checksum=@hash;
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_file_receipt', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_file_receipt AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_file_receipt
    @id uniqueidentifier,
    @institutionId int,
    @channel varchar(12),
    @originalFilename nvarchar(max),
    @originalPath nvarchar(max),
    @claimedPath nvarchar(max),
    @bytes varbinary(max),
    @length bigint,
    @hash binary(32),
    @canonical binary(32),
    @outcome varchar(20),
    @location varchar(20),
    @definition varchar(32),
    @errorType varchar(64),
    @validationJson nvarchar(max),
    @duplicate uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_file_receipts(id,intake_institution_id,effective_institution_id,channel,original_filename,original_path,current_path,received_utc,source_bytes,byte_length,checksum,canonical_checksum,import_state,location_state,detected_definition,error_type,validation_json,duplicate_of)
     VALUES(@id,@institutionId,@institutionId,@channel,@originalFilename,@originalPath,@claimedPath,SYSUTCDATETIME(),@bytes,@length,@hash,@canonical,@outcome,@location,@definition,@errorType,@validationJson,@duplicate);
END;
GO

IF OBJECT_ID(N'dbo.sp_get_response_mapping', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_response_mapping AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_response_mapping
    @institutionId int,
    @input varchar(32)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT output_code AS OutputCode,revision FROM dbo.t_response_mappings
     WHERE institution_id=@institutionId AND input_code=@input;
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_batch', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_batch AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_batch
    @batchId uniqueidentifier,
    @id uniqueidentifier,
    @institutionId int,
    @input varchar(32),
    @output varchar(32),
    @revision int,
    @messageId nvarchar(100),
    @creation nvarchar(64)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_payment_batches(id,receipt_id,effective_institution_id,input_code,output_code,mapping_revision,original_message_id,original_creation,state,created_utc)
     VALUES(@batchId,@id,@institutionId,@input,@output,@revision,@messageId,@creation,'Editable',SYSUTCDATETIME());
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_batch_categories', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_batch_categories AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_batch_categories
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_batch_categories(batch_id,category)
     VALUES(@batchId,'LOCAL'),(@batchId,'ACH'),(@batchId,'RTGS'),(@batchId,'SWIFT'),(@batchId,'INVALID');
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_payment_group', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_payment_group AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_payment_group
    @groupId uniqueidentifier,
    @batchId uniqueidentifier,
    @ordinal int,
    @paymentInfoId nvarchar(100),
    @debtorName nvarchar(500),
    @debtorAccount nvarchar(200),
    @debtorAccountKind varchar(20),
    @debtorCurrency char(3),
    @debtorAgent nvarchar(200),
    @executionDate nvarchar(64),
    @sourceXml nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_payment_groups(id,batch_id,source_ordinal,payment_info_id,debtor_name,debtor_account_id,debtor_account_kind,debtor_account_currency,debtor_agent,requested_execution_date,source_xml)
     VALUES(@groupId,@batchId,@ordinal,@paymentInfoId,@debtorName,@debtorAccount,@debtorAccountKind,@debtorCurrency,@debtorAgent,@executionDate,@sourceXml);
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_transfer', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_transfer AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_transfer
    @transferId uniqueidentifier,
    @batchId uniqueidentifier,
    @groupId uniqueidentifier,
    @ordinal int,
    @instructionId nvarchar(100),
    @endToEndId nvarchar(100),
    @creditorName nvarchar(500),
    @creditorAccount nvarchar(200),
    @creditorAccountKind varchar(20),
    @creditorAgent nvarchar(200),
    @instructedAmount decimal(28,8),
    @instructedCurrency char(3),
    @equivalentAmount decimal(28,8),
    @equivalentCurrency char(3),
    @transferCurrency char(3),
    @accountCurrency char(3),
    @routingAmount decimal(28,8),
    @country char(2),
    @bankCode char(4),
    @ruleRevision int,
    @routingCode varchar(64),
    @routingExplanation nvarchar(1000),
    @category varchar(8),
    @decision varchar(8),
    @source varchar(8),
    @reason nvarchar(2000),
    @actor nvarchar(max),
    @decided datetime2(3),
    @sourceXml nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_transfers(id,batch_id,group_id,source_ordinal,instruction_id,end_to_end_id,creditor_name,creditor_account_id,creditor_account_kind,creditor_agent,instructed_amount,instructed_currency,equivalent_amount,equivalent_currency,transfer_currency,account_currency,routing_amount,iban_country,iban_bank_code,rule_revision,routing_code,routing_explanation,category,decision,decision_source,decision_reason,decision_actor,decision_utc,source_xml)
     VALUES(@transferId,@batchId,@groupId,@ordinal,@instructionId,@endToEndId,@creditorName,@creditorAccount,@creditorAccountKind,@creditorAgent,@instructedAmount,@instructedCurrency,@equivalentAmount,@equivalentCurrency,@transferCurrency,@accountCurrency,@routingAmount,@country,@bankCode,@ruleRevision,@routingCode,@routingExplanation,@category,@decision,@source,@reason,@actor,@decided,@sourceXml);
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_import_error_document', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_import_error_document AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_import_error_document
    @docId uniqueidentifier,
    @id uniqueidentifier,
    @institutionId int,
    @filename nvarchar(max),
    @report varbinary(max),
    @contentHash binary(32)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_outgoing_documents(id,kind,receipt_id,recipient_institution_id,filename,mime_type,content_bytes,content_sha256,generated_utc,publication_state)
     VALUES(@docId,'ImportErrorJson',@id,@institutionId,@filename,'application/json',@report,@contentHash,SYSUTCDATETIME(),'Pending');
END;
GO

IF OBJECT_ID(N'dbo.sp_get_institution_code', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_institution_code AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_institution_code
    @institutionId int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT code FROM dbo.t_institutions WHERE id=@institutionId;
END;
GO

IF OBJECT_ID(N'dbo.sp_update_receipt_location', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_update_receipt_location AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_update_receipt_location
    @target nvarchar(max),
    @state nvarchar(128),
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_file_receipts SET current_path=@target,location_state=@state WHERE id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_pending_receipt_moves', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_pending_receipt_moves AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_pending_receipt_moves
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id,current_path AS CurrentPath,intake_institution_id AS InstitutionId,
     import_state AS ImportState FROM dbo.t_file_receipts WHERE location_state IN ('ArchivePending','ErrorMovePending');
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_audit_event', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_audit_event AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_audit_event
    @actor nvarchar(max),
    @action varchar(64),
    @receiptId uniqueidentifier,
    @batchId uniqueidentifier,
    @transferId uniqueidentifier,
    @documentId uniqueidentifier,
    @json nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_workflow_audit(actor,action,receipt_id,batch_id,transfer_id,document_id,details_json)
     VALUES(@actor,@action,@receiptId,@batchId,@transferId,@documentId,@json);
END;
GO

IF OBJECT_ID(N'dbo.sp_get_batch_id_for_receipt', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_batch_id_for_receipt AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_batch_id_for_receipt
    @receiptId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id FROM dbo.t_payment_batches WHERE receipt_id=@receiptId;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_file_receipts', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_file_receipts AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_file_receipts
    @institutionId int,
    @state nvarchar(128),
    @pattern nvarchar(4000),
    @skip int,
    @take int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT r.id,r.original_filename AS originalFilename,ri.code AS intakeInstitution,ei.code AS effectiveInstitution,
     r.channel,r.received_utc AS receivedUtc,r.import_state AS importState,r.location_state AS locationState,
     r.error_type AS errorType,r.duplicate_of AS duplicateOf,b.id AS batchId,b.state AS batchState,
     r.detected_definition AS detectedDefinition,r.source_bytes AS sourceBytes,b.output_code AS outputCode,
     (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id) AS transferCount,
     (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Pending') AS pendingCount
     FROM dbo.t_file_receipts r JOIN dbo.t_institutions ri ON ri.id=r.intake_institution_id
     JOIN dbo.t_institutions ei ON ei.id=r.effective_institution_id
     LEFT JOIN dbo.t_payment_batches b ON b.receipt_id=r.id
     WHERE (@institutionId IS NULL OR r.effective_institution_id=@institutionId) AND (@state IS NULL OR r.import_state=@state OR b.state=@state)
     AND (@pattern IS NULL OR r.original_filename LIKE @pattern OR b.original_message_id LIKE @pattern)
     ORDER BY r.received_utc DESC,r.id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_file_receipt', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_file_receipt AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_file_receipt
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT r.id,r.original_filename AS originalFilename,r.original_path AS originalPath,r.current_path AS currentPath,
     ri.code AS intakeInstitution,ei.code AS effectiveInstitution,r.channel,r.received_utc AS receivedUtc,r.byte_length AS byteLength,
     CONVERT(varchar(64),r.checksum,2) AS checksum,r.import_state AS importState,r.location_state AS locationState,
     r.detected_definition AS detectedDefinition,r.source_bytes AS sourceBytes,r.error_type AS errorType,r.validation_json AS validationJson,r.duplicate_of AS duplicateOf,
     b.id AS batchId,e.id AS technicalDocumentId,e.publication_state AS technicalPublicationState,
     CONVERT(varchar(18),CONVERT(varbinary(8),r.rv),1) AS version
     FROM dbo.t_file_receipts r JOIN dbo.t_institutions ri ON ri.id=r.intake_institution_id
     JOIN dbo.t_institutions ei ON ei.id=r.effective_institution_id LEFT JOIN dbo.t_payment_batches b ON b.receipt_id=r.id
     LEFT JOIN dbo.t_outgoing_documents e ON e.receipt_id=r.id AND e.kind='ImportErrorJson' WHERE r.id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_original_file', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_original_file AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_original_file
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT source_bytes AS Bytes,original_filename AS Filename FROM dbo.t_file_receipts WHERE id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_batch', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_batch AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_batch
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT b.id,b.receipt_id AS receiptId,i.code AS institution,b.input_code AS inputCode,
     b.output_code AS outputCode,b.mapping_revision AS mappingRevision,b.original_message_id AS originalMessageId,
     b.original_creation AS originalCreation,b.state,b.current_revision AS currentRevision,b.assignment_locked_utc AS assignmentLockedUtc,
     b.created_utc AS createdUtc,CONVERT(varchar(18),CONVERT(varbinary(8),b.rv),1) AS version,
     (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id) AS transferCount,
     (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Pending') AS pendingCount,
     (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Rejected' AND t.decision_source='System') AS systemRejectedCount,
     (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Rejected' AND t.decision_source='Operator') AS operatorRejectedCount
     FROM dbo.t_payment_batches b JOIN dbo.t_institutions i ON i.id=b.effective_institution_id WHERE b.id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_batch_categories', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_batch_categories AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_batch_categories
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.category,COUNT(t.id) AS transferCount,
     CASE WHEN c.category='INVALID' THEN 'NotRequired' WHEN COUNT(t.id)=0 THEN 'NotApplicable'
     WHEN c.processed_utc IS NULL THEN 'NotProcessed' ELSE 'Processed' END AS processingState,
     c.processed_utc AS processedUtc,c.processed_actor AS processedActor
     FROM dbo.t_batch_categories c LEFT JOIN dbo.t_transfers t ON t.batch_id=c.batch_id AND t.category=c.category
     WHERE c.batch_id=@id GROUP BY c.category,c.processed_utc,c.processed_actor
     ORDER BY CASE c.category WHEN 'LOCAL' THEN 1 WHEN 'ACH' THEN 2 WHEN 'RTGS' THEN 3 WHEN 'SWIFT' THEN 4 ELSE 5 END;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_currency_totals', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_currency_totals AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_currency_totals
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT currency,CONVERT(varchar(50),SUM(amount)) AS amount
     FROM (SELECT instructed_currency AS currency,instructed_amount AS amount FROM dbo.t_transfers WHERE batch_id=@id AND instructed_amount IS NOT NULL
     UNION ALL SELECT equivalent_currency,equivalent_amount FROM dbo.t_transfers WHERE batch_id=@id AND equivalent_amount IS NOT NULL) amounts
     GROUP BY currency ORDER BY currency;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_payment_groups', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_payment_groups AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_payment_groups
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT g.id,g.source_ordinal AS sourceOrdinal,g.payment_info_id AS paymentInfoId,g.debtor_name AS debtorName,
     g.debtor_account_id AS debtorAccount,g.debtor_account_kind AS debtorAccountKind,g.debtor_account_currency AS debtorCurrency,
     g.debtor_agent AS debtorAgent,g.requested_execution_date AS executionDate,COUNT(t.id) AS transferCount
     FROM dbo.t_payment_groups g LEFT JOIN dbo.t_transfers t ON t.group_id=g.id WHERE g.batch_id=@id
     GROUP BY g.id,g.source_ordinal,g.payment_info_id,g.debtor_name,g.debtor_account_id,g.debtor_account_kind,g.debtor_account_currency,g.debtor_agent,g.requested_execution_date
     ORDER BY g.source_ordinal;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_transfers', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_transfers AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_transfers
    @id uniqueidentifier,
    @category varchar(8),
    @decision varchar(8),
    @groupId uniqueidentifier,
    @skip int,
    @take int
AS
BEGIN
    SET NOCOUNT ON;

    SELECT t.id,t.group_id AS groupId,g.source_ordinal AS groupOrdinal,t.source_ordinal AS sourceOrdinal,
     g.payment_info_id AS paymentInfoId,g.debtor_account_id AS debtorAccount,t.instruction_id AS instructionId,t.end_to_end_id AS endToEndId,
     t.creditor_name AS creditorName,t.creditor_account_id AS creditorAccount,t.creditor_account_kind AS creditorAccountKind,
     CONVERT(varchar(50),COALESCE(t.instructed_amount,t.equivalent_amount)) AS amount,
     COALESCE(t.instructed_currency,t.equivalent_currency) AS currency,t.transfer_currency AS transferCurrency,
     CASE WHEN t.instructed_amount IS NOT NULL THEN 'Instructed' ELSE 'Equivalent' END AS amountBasis,
     t.category,t.decision,t.decision_source AS decisionSource,t.decision_reason AS decisionReason,
     t.routing_code AS routingCode,t.routing_explanation AS routingExplanation,CONVERT(varchar(18),CONVERT(varbinary(8),t.rv),1) AS version
     FROM dbo.t_transfers t JOIN dbo.t_payment_groups g ON g.id=t.group_id WHERE t.batch_id=@id
     AND (@category IS NULL OR t.category=@category) AND (@decision IS NULL OR t.decision=@decision)
     AND (@groupId IS NULL OR t.group_id=@groupId)
     ORDER BY g.source_ordinal,t.source_ordinal OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_transfer', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_transfer AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_transfer
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT t.id,t.batch_id AS batchId,t.group_id AS groupId,g.source_ordinal AS groupOrdinal,
     t.source_ordinal AS sourceOrdinal,g.payment_info_id AS paymentInfoId,g.debtor_name AS debtorName,g.debtor_account_id AS debtorAccount,
     t.instruction_id AS instructionId,t.end_to_end_id AS endToEndId,t.creditor_name AS creditorName,
     t.creditor_account_id AS creditorAccount,t.creditor_account_kind AS creditorAccountKind,t.creditor_agent AS creditorAgent,
     CONVERT(varchar(50),t.instructed_amount) AS instructedAmount,t.instructed_currency AS instructedCurrency,
     CONVERT(varchar(50),t.equivalent_amount) AS equivalentAmount,t.equivalent_currency AS equivalentCurrency,
     t.transfer_currency AS transferCurrency,t.account_currency AS accountCurrency,CONVERT(varchar(50),t.routing_amount) AS routingAmount,
     t.iban_country AS ibanCountry,t.iban_bank_code AS ibanBankCode,t.rule_revision AS ruleRevision,t.routing_code AS routingCode,
     t.routing_explanation AS routingExplanation,t.category,t.decision,t.decision_source AS decisionSource,
     t.decision_reason AS decisionReason,t.decision_actor AS decisionActor,t.decision_utc AS decisionUtc,
     t.source_xml AS sourceXml,CONVERT(varchar(18),CONVERT(varbinary(8),t.rv),1) AS version
     FROM dbo.t_transfers t JOIN dbo.t_payment_groups g ON g.id=t.group_id WHERE t.id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_transfer_audit', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_transfer_audit AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_transfer_audit
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT occurred_utc AS occurredUtc,actor,action,details_json AS detailsJson
     FROM dbo.t_workflow_audit WHERE transfer_id=@id ORDER BY id;
END;
GO

IF OBJECT_ID(N'dbo.sp_count_category_transfers', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_count_category_transfers AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_count_category_transfers
    @batchId uniqueidentifier,
    @category varchar(8)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) FROM dbo.t_transfers WHERE batch_id=@batchId AND category=@category;
END;
GO

IF OBJECT_ID(N'dbo.sp_mark_category_processed', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_mark_category_processed AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_mark_category_processed
    @actor nvarchar(max),
    @batchId uniqueidentifier,
    @category varchar(8)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_batch_categories SET processed_utc=SYSUTCDATETIME(),processed_actor=@actor
     WHERE batch_id=@batchId AND category=@category AND processed_utc IS NULL;
     SELECT @@ROWCOUNT;
END;
GO

IF OBJECT_ID(N'dbo.sp_lock_batch_assignment', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_lock_batch_assignment AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_lock_batch_assignment
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_payment_batches SET assignment_locked_utc=COALESCE(assignment_locked_utc,SYSUTCDATETIME()) WHERE id=@batchId;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_decision_transfers', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_decision_transfers AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_decision_transfers
    @ids dbo.t_transfer_id_list READONLY
AS
BEGIN
    SET NOCOUNT ON;

    SELECT t.id,t.batch_id AS BatchId,t.category,t.decision,t.decision_reason AS Reason,
     t.rv AS Version,c.processed_utc AS ProcessedUtc FROM dbo.t_transfers t
     JOIN dbo.t_batch_categories c ON c.batch_id=t.batch_id AND c.category=t.category JOIN @ids selected ON selected.id=t.id;
END;
GO

IF OBJECT_ID(N'dbo.sp_update_transfer_decision', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_update_transfer_decision AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_update_transfer_decision
    @decision varchar(8),
    @reason nvarchar(2000),
    @actor nvarchar(max),
    @id uniqueidentifier,
    @version binary(8)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_transfers SET decision=@decision,decision_source='Operator',decision_reason=@reason,
     decision_actor=@actor,decision_utc=SYSUTCDATETIME() WHERE id=@id AND rv=@version;
END;
GO

IF OBJECT_ID(N'dbo.sp_bump_batch_version', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_bump_batch_version AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_bump_batch_version
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_payment_batches SET state=state WHERE id=@batchId;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_receipt_checksum', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_receipt_checksum AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_receipt_checksum
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT checksum FROM dbo.t_file_receipts WHERE id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_assign_receipt', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_assign_receipt AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_assign_receipt
    @targetId int,
    @receiptId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_file_receipts SET effective_institution_id=@targetId WHERE id=@receiptId;
END;
GO

IF OBJECT_ID(N'dbo.sp_assign_batch', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_assign_batch AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_assign_batch
    @targetId int,
    @output varchar(32),
    @revision int,
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_payment_batches SET effective_institution_id=@targetId,output_code=@output,mapping_revision=@revision WHERE id=@batchId;
END;
GO

IF OBJECT_ID(N'dbo.sp_find_report_operation', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_find_report_operation AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_find_report_operation
    @operationId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id FROM dbo.t_outgoing_documents WHERE operation_id=@operationId;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_report_transfers', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_report_transfers AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_report_transfers
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT g.id AS GroupId,g.source_ordinal AS GroupOrdinal,g.payment_info_id AS PaymentInfoId,
     t.source_ordinal AS TransferOrdinal,t.instruction_id AS InstructionId,t.end_to_end_id AS EndToEndId,t.decision,t.decision_reason AS Reason
     FROM dbo.t_payment_groups g JOIN dbo.t_transfers t ON t.group_id=g.id WHERE g.batch_id=@batchId ORDER BY g.source_ordinal,t.source_ordinal;
END;
GO

IF OBJECT_ID(N'dbo.sp_insert_status_report', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_insert_status_report AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_insert_status_report
    @docId uniqueidentifier,
    @receiptId uniqueidentifier,
    @batchId uniqueidentifier,
    @revision int,
    @operationId uniqueidentifier,
    @institutionId int,
    @outputCode varchar(32),
    @messageId nvarchar(100),
    @filename nvarchar(max),
    @bytes varbinary(max),
    @hash binary(32)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.t_outgoing_documents(id,kind,receipt_id,batch_id,revision,operation_id,recipient_institution_id,iso_definition,message_id,filename,mime_type,content_bytes,content_sha256,generated_utc,publication_state)
     VALUES(@docId,'Pain002',@receiptId,@batchId,@revision,@operationId,@institutionId,@outputCode,@messageId,@filename,'application/xml',@bytes,@hash,SYSUTCDATETIME(),'Pending');
END;
GO

IF OBJECT_ID(N'dbo.sp_finalize_batch', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_finalize_batch AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_finalize_batch
    @revision int,
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_payment_batches SET state='Finalized',current_revision=@revision WHERE id=@batchId;
END;
GO

IF OBJECT_ID(N'dbo.sp_count_publishing_reports', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_count_publishing_reports AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_count_publishing_reports
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) FROM dbo.t_outgoing_documents WHERE batch_id=@batchId AND publication_state='Publishing';
END;
GO

IF OBJECT_ID(N'dbo.sp_supersede_unpublished_reports', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_supersede_unpublished_reports AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_supersede_unpublished_reports
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_outgoing_documents SET publication_state='SupersededUnpublished' WHERE batch_id=@batchId AND publication_state IN ('Pending','Failed');
END;
GO

IF OBJECT_ID(N'dbo.sp_reopen_batch', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_reopen_batch AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_reopen_batch
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_payment_batches SET state='Editable' WHERE id=@batchId;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_batch_reports', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_batch_reports AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_batch_reports
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT d.id,d.revision,d.iso_definition AS isoDefinition,d.message_id AS messageId,
     i.code AS recipient,d.filename,d.generated_utc AS generatedUtc,d.publication_state AS publicationState,
     d.published_utc AS publishedUtc,d.retry_count AS retryCount,d.last_error AS lastError
     FROM dbo.t_outgoing_documents d JOIN dbo.t_institutions i ON i.id=d.recipient_institution_id
     WHERE d.batch_id=@batchId ORDER BY d.revision DESC;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_outgoing_document', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_outgoing_document AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_outgoing_document
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT d.id,d.kind,d.receipt_id AS ReceiptId,d.batch_id AS BatchId,
     d.filename,d.mime_type AS MimeType,d.content_bytes AS Bytes,d.content_sha256 AS Hash,d.publication_state AS PublicationState,
     i.code AS InstitutionCode FROM dbo.t_outgoing_documents d JOIN dbo.t_institutions i ON i.id=d.recipient_institution_id WHERE d.id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_retry_publication', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_retry_publication AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_retry_publication
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_outgoing_documents SET publication_state='Pending',last_error=NULL,next_attempt_utc=NULL
     WHERE id=@id AND publication_state='Failed';
     SELECT @@ROWCOUNT;
END;
GO

IF OBJECT_ID(N'dbo.sp_list_batch_audit', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_batch_audit AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_batch_audit
    @batchId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id,occurred_utc AS occurredUtc,actor,action,receipt_id AS receiptId,
     transfer_id AS transferId,document_id AS documentId,details_json AS detailsJson
     FROM dbo.t_workflow_audit WHERE batch_id=@batchId ORDER BY id;
END;
GO

IF OBJECT_ID(N'dbo.sp_get_batch_readiness', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_get_batch_readiness AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_get_batch_readiness
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT state,output_code AS OutputCode FROM dbo.t_payment_batches WHERE id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_count_pending_transfers', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_count_pending_transfers AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_count_pending_transfers
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(*) FROM dbo.t_transfers WHERE batch_id=@id AND decision='Pending';
END;
GO

IF OBJECT_ID(N'dbo.sp_list_unprocessed_categories', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_list_unprocessed_categories AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_list_unprocessed_categories
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.category FROM dbo.t_batch_categories c WHERE c.batch_id=@id
     AND c.category<>'INVALID' AND c.processed_utc IS NULL AND EXISTS(SELECT 1 FROM dbo.t_transfers t WHERE t.batch_id=c.batch_id AND t.category=c.category);
END;
GO

IF OBJECT_ID(N'dbo.sp_lock_batch', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_lock_batch AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_lock_batch
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id,receipt_id AS ReceiptId,effective_institution_id AS InstitutionId,input_code AS InputCode,
     output_code AS OutputCode,original_message_id AS OriginalMessageId,original_creation AS OriginalCreation,
     state,current_revision AS CurrentRevision,assignment_locked_utc AS AssignmentLockedUtc,rv AS Version
     FROM dbo.t_payment_batches WITH(UPDLOCK,HOLDLOCK) WHERE id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_claim_outgoing_document', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_claim_outgoing_document AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_claim_outgoing_document
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH candidate AS (
     SELECT TOP(1) * FROM dbo.t_outgoing_documents WITH(UPDLOCK,READPAST)
     WHERE (publication_state IN ('Pending','Failed') AND (next_attempt_utc IS NULL OR next_attempt_utc<=SYSUTCDATETIME()))
     OR (publication_state='Publishing' AND publication_started_utc<DATEADD(minute,-2,SYSUTCDATETIME()))
     ORDER BY generated_utc,id)
     UPDATE candidate SET publication_state='Publishing',publication_started_utc=SYSUTCDATETIME()
     OUTPUT inserted.id;
END;
GO

IF OBJECT_ID(N'dbo.sp_mark_document_published', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_mark_document_published AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_mark_document_published
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_outgoing_documents SET publication_state='PublishedToOut',published_utc=SYSUTCDATETIME(),
     last_error=NULL,next_attempt_utc=NULL WHERE id=@id;
END;
GO

IF OBJECT_ID(N'dbo.sp_mark_publication_failed', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE dbo.sp_mark_publication_failed AS RETURN 0;');
GO
ALTER PROCEDURE dbo.sp_mark_publication_failed
    @error nvarchar(2000),
    @id uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.t_outgoing_documents SET publication_state='Failed',retry_count=retry_count+1,
     last_error=@error,next_attempt_utc=DATEADD(second,30,SYSUTCDATETIME()) WHERE id=@id;
END;
GO
