function(GFPIR="IRA")
{
	
	
	"name": "GDS_AzureBlobStorage_Excel_" + GFPIR,
	"properties": {
		"linkedServiceName": {
			"referenceName": "GLS_AzureBlobStorage_" + GFPIR,
			"type": "LinkedServiceReference",
			"parameters": {
				"StorageAccountEndpoint": {
					"value": "@dataset().StorageAccountEndpoint",
					"type": "Expression"
				}
			}
		},
		"parameters": {
			"RelativePath": {
				"type": "string"
			},
			"FileName": {
				"type": "string"
			},
			"StorageAccountEndpoint": {
				"type": "string"
			},
			"StorageAccountContainerName": {
				"type": "string"
			},
			"SheetName": {
				"type": "string"
			},
			"FirstRowAsHeader": {
				"type": "bool"
			}
		},
		"folder": {
			"name": "ADS Go Fast/Generic/"  + GFPIR
		},
		"annotations": [],
		"type": "Excel",
		"typeProperties": {
			"sheetName": {
				"value": "@dataset().SheetName",
				"type": "Expression"
			},
			"location": {
				"type": "AzureBlobStorageLocation",
				"fileName": {
					"value": "@dataset().FileName",
					"type": "Expression"
				},
				"folderPath": {
					"value": "@dataset().RelativePath",
					"type": "Expression"
				},
				"container": {
					"value": "@dataset().StorageAccountContainerName",
					"type": "Expression"
				}
			},
			"firstRowAsHeader": {
				"value": "@dataset().FirstRowAsHeader",
				"type": "Expression"
			}
		},
		"schema": []
	},
	"type": "Microsoft.DataFactory/factories/datasets"
}




