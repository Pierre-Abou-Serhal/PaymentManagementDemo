/* PaymentManagementDemo configuration. Apply after 001_schema.sql. No payment outcomes are seeded. */
USE PaymentManagementDemo;
GO
SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON; SET QUOTED_IDENTIFIER ON; SET NUMERIC_ROUNDABORT OFF;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.t_institutions WHERE code=N'ALFA') INSERT dbo.t_institutions(code,display_name) VALUES(N'ALFA',N'ALFA');
IF NOT EXISTS(SELECT 1 FROM dbo.t_institutions WHERE code=N'AZADEYA') INSERT dbo.t_institutions(code,display_name) VALUES(N'AZADEYA',N'AZADEYA');
GO
IF NOT EXISTS(SELECT 1 FROM dbo.t_demo_routing_rules WHERE currency='EUR') INSERT dbo.t_demo_routing_rules(currency,threshold) VALUES('EUR',10000);
IF NOT EXISTS(SELECT 1 FROM dbo.t_demo_routing_rules WHERE currency='USD') INSERT dbo.t_demo_routing_rules(currency,threshold) VALUES('USD',10000);
IF NOT EXISTS(SELECT 1 FROM dbo.t_demo_routing_rules WHERE currency='GBP') INSERT dbo.t_demo_routing_rules(currency,threshold) VALUES('GBP',10000);
IF NOT EXISTS(SELECT 1 FROM dbo.t_demo_routing_rules WHERE currency='LBP') INSERT dbo.t_demo_routing_rules(currency,threshold) VALUES('LBP',1000000000);
GO
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.01')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.01','pain.001','urn:iso:std:iso:20022:xsd:pain.001.001.01','pain.001.001.01.xsd',CONVERT(binary(32),'E12A9B8A74546559CC7B39A598C31B49A8617135117095685BC5311069B56F7C',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.01.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.002.01')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.002.01','pain.001','urn:iso:std:iso:20022:xsd:pain.001.002.01','pain.001.002.01.xsd',CONVERT(binary(32),'EB43E85D4F0E4F69200CFC1FCE0F799BB8C4D64C45DA99AF9D90D19EAC7128B2',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.002.01.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.003.01')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.003.01','pain.001','urn:iso:std:iso:20022:xsd:pain.001.003.01','pain.001.003.01.xsd',CONVERT(binary(32),'9156CB9A8ED6059B857A2E7CEB1BECFB996C274C93E0B9FE4C5AA5CDF3D8C4FE',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.003.01.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.02')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.02','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.02','pain.001.001.02.xsd',CONVERT(binary(32),'FC391C30D5F1ECC88C7D7790EA3472AB0228B2FDF3EBFDEB0203933F546CE503',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.02.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.03')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.03','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.03','pain.001.001.03.xsd',CONVERT(binary(32),'AE2BBBA02A6BE0119A26F4AFCB65CED067453CB1B81D38B26BCC569F19ECA93E',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.03.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.04')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.04','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.04','pain.001.001.04.xsd',CONVERT(binary(32),'369BB931E2EDE491D81AA42AC41E8E39C86A869B33D6AEC208F43CCBC6E6D4E6',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.04.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.05')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.05','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.05','pain.001.001.05.xsd',CONVERT(binary(32),'DE9C2103BB804CABBA156D8B223B0696273DC396877B38612BB56FB1B2C67198',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.05.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.06')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.06','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.06','pain.001.001.06.xsd',CONVERT(binary(32),'9A61359A11B009CF53EB7AA0395FA6103CE18DA4C97028CB62C0549BFA3CA55F',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.06.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.07')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.07','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.07','pain.001.001.07.xsd',CONVERT(binary(32),'AE33AB72C653BBE960F5B5B35C66D371A3DF2C5422C4E58DA9AEDAAD56F8B8C5',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.07.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.08')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.08','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.08','pain.001.001.08.xsd',CONVERT(binary(32),'A6CD3EF0D7961654291DAF532DE3D38208ED7F4A704250D9F04E44C9949A5170',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.08.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.09')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.09','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.09','pain.001.001.09.xsd',CONVERT(binary(32),'05440A7E84F695E7DBC6677082F43623CD7393881B76A5E8D648AE21D7D728E3',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.09.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.10')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.10','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.10','pain.001.001.10.xsd',CONVERT(binary(32),'90CFB613C7BC11D8BEC4E9049AF404FF2C28FB2275621F145A302A6D2494C09C',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.10.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.11')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.11','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.11','pain.001.001.11.xsd',CONVERT(binary(32),'136A8A76E4CC4C94BB9F12057232268D50CF09FECA8351EEA383A3B29604AED2',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.11.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.12')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.12','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.12','pain.001.001.12.xsd',CONVERT(binary(32),'7B045A27AD631255E5B5C9235046363A24792ECAD774B3A18270DA86AD6D8B2C',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.12.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.001.001.13')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.001.001.13','pain.001','urn:iso:std:iso:20022:tech:xsd:pain.001.001.13','pain.001.001.13.xsd',CONVERT(binary(32),'A840B45EF45FB0E25CA3C2299F5EFBA4F71C4C2100236052FC9AF194FC7FDEA5',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.001.001.13.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.01')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.01','pain.002','urn:iso:std:iso:20022:xsd:pain.002.001.01','pain.002.001.01.xsd',CONVERT(binary(32),'C7F10B3F5650031B7C64DAD810AE1080FE6C8BE843900FA037877151E440E893',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.01.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.02')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.02','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.02','pain.002.001.02.xsd',CONVERT(binary(32),'514D41ED04E2615C9A26564DD618DBD4106D0E7E47D13051951042BA9436DC38',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.02.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.03')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.03','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.03','pain.002.001.03.xsd',CONVERT(binary(32),'C0C2B98F51638147598678BBEC3D5AEDFA270A819C2FDA8014BA3C34A5972195',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.03.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.04')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.04','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.04','pain.002.001.04.xsd',CONVERT(binary(32),'A029E7703D5300CCB4637C4081B7935889DFC0DE519B128EB8D01BE689DB944F',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.04.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.05')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.05','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.05','pain.002.001.05.xsd',CONVERT(binary(32),'A542ADC94354B202B73246179559BC4F050DD5E71646A3F37E284DB761A84BB1',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.05.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.06')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.06','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.06','pain.002.001.06.xsd',CONVERT(binary(32),'BBF5FC8BA112432B5CC6178D9E93C51560A99854AA9549F3159680A712A80ADC',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.06.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.07')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.07','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.07','pain.002.001.07.xsd',CONVERT(binary(32),'7AB586DEAE2240FB8A7F7BD73D476E1377E53A6D1891644CD278328BCC80F28D',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.07.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.08')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.08','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.08','pain.002.001.08.xsd',CONVERT(binary(32),'09480FE46F7AC8D1545E1DE5BB9CA3FBD9F9508481AF8E49FFB01CD4156C8568',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.08.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.09')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.09','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.09','pain.002.001.09.xsd',CONVERT(binary(32),'5CFA2AAC1EC31839CBA4EC13BB3AC85B40B22A8DC2A5A05A3535522662A32017',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.09.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.10')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.10','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.10','pain.002.001.10.xsd',CONVERT(binary(32),'22A9DDA9D006C21FD368F44E9FAE812E7B5CDE95B6774F31289C372242AAC906',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.10.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.11')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.11','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.11','pain.002.001.11.xsd',CONVERT(binary(32),'D7D5C787FB027A2025193984D9F2AFAC6AEC15BEBD368623DD0156B6A8D38B0D',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.11.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.12')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.12','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.12','pain.002.001.12.xsd',CONVERT(binary(32),'13762890A02C44B05EA2FA4285B7C2E159A3BF4D518C8C11116CB4A2FAA9F899',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.12.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.13')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.13','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.13','pain.002.001.13.xsd',CONVERT(binary(32),'B143DD06305EF67C13CE65ECF8E7ACE1732792FAF556DD1A8030D154342D7906',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.13.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.14')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.14','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.14','pain.002.001.14.xsd',CONVERT(binary(32),'1FB79118C5FA2DBF9F20AA8CA0877AAFE96067E6E620689342A847D9DE7B1DE7',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.14.xsd',1);
IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='pain.002.001.15')
 INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)
 VALUES('pain.002.001.15','pain.002','urn:iso:std:iso:20022:tech:xsd:pain.002.001.15','pain.002.001.15.xsd',CONVERT(binary(32),'F8584D291515D53AF48084DFC57BE55CF428DF6AD78C941697A937C25C38D7E3',2),'https://github.com/teghnet/iso20022/blob/main/xsd/pain.002.001.15.xsd',1);
GO
DECLARE @alfa int = (SELECT id FROM dbo.t_institutions WHERE code=N'ALFA');
DECLARE @azadeya int = (SELECT id FROM dbo.t_institutions WHERE code=N'AZADEYA');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.01')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.01','pain.002.001.01');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.01')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.01','pain.002.001.01');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.002.01')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.002.01','pain.002.001.01');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.002.01')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.002.01','pain.002.001.01');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.003.01')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.003.01','pain.002.001.01');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.003.01')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.003.01','pain.002.001.01');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.02')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.02','pain.002.001.02');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.02')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.02','pain.002.001.02');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.03')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.03','pain.002.001.03');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.03')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.03','pain.002.001.03');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.04')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.04','pain.002.001.04');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.04')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.04','pain.002.001.04');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.05')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.05','pain.002.001.05');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.05')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.05','pain.002.001.05');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.06')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.06','pain.002.001.06');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.06')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.06','pain.002.001.06');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.07')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.07','pain.002.001.08');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.07')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.07','pain.002.001.08');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.08')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.08','pain.002.001.09');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.08')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.08','pain.002.001.09');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.09')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.09','pain.002.001.10');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.09')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.09','pain.002.001.10');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.10')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.10','pain.002.001.11');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.10')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.10','pain.002.001.11');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.11')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.11','pain.002.001.13');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.11')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.11','pain.002.001.13');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.12')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.12','pain.002.001.14');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.12')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.12','pain.002.001.14');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@alfa AND input_code='pain.001.001.13')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@alfa,'pain.001.001.13','pain.002.001.15');
IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id=@azadeya AND input_code='pain.001.001.13')
 INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@azadeya,'pain.001.001.13','pain.002.001.15');
GO
