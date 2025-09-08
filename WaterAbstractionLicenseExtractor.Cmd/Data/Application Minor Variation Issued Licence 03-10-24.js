window.aiData['ApplicationMinorVariationIssuedLicence031024'] = {
  "id": "22712261-LV20241003",
  "licenceNumber": "2/27/12/261",
  "filename": "Application Minor Variation Issued Licence 03.10.24.pdf",
  "licenceVersion": {
    "licenceVersionId": "LV20241003",
    "effectiveDate": "2024-10-03T00:00:00",
    "issueDate": "2024-10-03T00:00:00",
    "issuer": "Environment Agency",
    "originalIssueDate": "1966-01-27T00:00:00"
  },
  "points": [
    {
      "purposeIds": [
        1
      ],
      "id": "SE039152",
      "description": "At National Grid Reference SE 039 152 marked \u2018A\u2019"
    },
    {
      "purposeIds": [
        2
      ],
      "id": "SE052166",
      "description": "At National Grid Reference SE 052 166 marked \u2018B\u2019"
    }
  ],
  "purposes": [
    {
      "pointIds": [
        1
      ],
      "id": "1",
      "description": "Public water supply"
    },
    {
      "pointIds": [
        2
      ],
      "id": "2",
      "description": "Transfer for the purpose of supplying water to the Huddersfield Narrow Canal"
    }
  ],
  "periodsOfAbstraction": [
    {
      "id": 1,
      "description": "All year",
      "pointIds": [
        "SE039152",
        "SE052166"
      ],
      "purposeIds": [
        "1",
        "2"
      ],
      "periodType": "notApplicable",
      "inclusive": true
    }
  ],
  "meansOfAbstraction": [
    {
      "id": 1,
      "description": "Gravity flow to and from a reservoir.",
      "limit": {
        "periodType": "notApplicable",
        "value": 0,
        "units": "",
        "points": [
          {
            "id": "",
            "description": ""
          }
        ],
        "purposes": [
          {
            "id": "",
            "description": ""
          }
        ],
        "implicitLimit": false
      }
    }
  ],
  "abstractionLimits": {
    "individual": [
      {
        "periodType": "notApplicable",
        "value": 730000,
        "units": "cubic metres per year",
        "points": [
          {
            "id": "SE039152",
            "description": "At National Grid Reference SE 039 152 marked \u2018A\u2019"
          }
        ],
        "purposes": [
          {
            "id": "1",
            "description": "Public water supply"
          }
        ],
        "implicitLimit": false
      },
      {
        "periodType": "notApplicable",
        "value": 2920000,
        "units": "cubic metres per year",
        "points": [
          {
            "id": "SE052166",
            "description": "At National Grid Reference SE 052 166 marked \u2018B\u2019"
          }
        ],
        "purposes": [
          {
            "id": "2",
            "description": "Transfer for the purpose of supplying water to the Huddersfield Narrow Canal"
          }
        ],
        "implicitLimit": false
      }
    ],
    "aggregates": [
      {
        "id": "-IL",
        "aggregateSetId": "",
        "primaryType": "inLicence",
        "naldType": "",
        "purposes": [],
        "points": [],
        "linkedLicences": [],
        "limits": []
      }
    ]
  },
  "definitionOfYear": {
    "periodType": "notApplicable",
    "startDate": "04-01",
    "endDate": "03-31",
    "inclusive": true
  }
};