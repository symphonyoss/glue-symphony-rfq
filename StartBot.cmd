@echo off
setlocal enabledelayedexpansion
set GLUE-ENV=LOCAL
set GLUE-REGION=TICK42
set T42_DIR=%CD%

SET prog="!CD!\GlueSymphonyRfqBridge\bin\Release\GlueSymphonyRfqBridge.exe"
SET cert="!CD!\GlueSymphonyRfqBridge\bin\Release\Config\nws.gluerfq-cert.p12"
SET certPwd="changeit"
START "RFQ Bridge" !prog! !cert! !certPwd!
