window.aiData['227290127003124'] = {
  "id": "368-LV19690423",
  "licenceNumber": "368",
  "filename": "2-27-29-012 7003124.PDF",
  "licenceVersion": {
    "licenceVersionId": "LV19690423",
    "effectiveDate": "1969-04-23T00:00:00",
    "issueDate": "1966-01-27T00:00:00",
    "issuer": "Yorkshire Water Authority",
    "originalIssueDate": "1966-01-27T00:00:00",
    "naldVersionNumber": "2"
  },
  "points": [
    {
      "naldId": "NZ 886 088",
      "purposeIds": [
        1
      ],
      "id": "1",
      "description": "River Esk at Ruswarp"
    },
    {
      "naldId": "NZ 873 082",
      "purposeIds": [
        1
      ],
      "id": "2",
      "description": "River Esk at Briggswath"
    }
  ],
  "purposes": [
    {
      "naldId": "1",
      "pointIds": [
        1,
        2
      ],
      "id": "1",
      "description": "Public Water Supply"
    }
  ],
  "periodsOfAbstraction": [
    {
      "id": 1,
      "description": "November to May, maximum rate of abstraction of 20.45 thousand cubic metres per day",
      "pointIds": [
        "1",
        "2"
      ],
      "purposeIds": [
        "1"
      ],
      "periodType": "notApplicable",
      "startDate": "11-01",
      "endDate": "05-31",
      "inclusive": true
    },
    {
      "id": 2,
      "description": "June to October, maximum rate of abstraction of 22.73 thousand cubic metres per day",
      "pointIds": [
        "1",
        "2"
      ],
      "purposeIds": [
        "1"
      ],
      "periodType": "notApplicable",
      "startDate": "06-01",
      "endDate": "10-31",
      "inclusive": true
    }
  ],
  "meansOfAbstraction": [
    {
      "id": 1,
      "description": "River intakes and pumps",
      "limit": {
        "periodType": "notApplicable",
        "value": 7823,
        "units": "Thousand cubic metres per annum",
        "points": [
          {
            "id": "1",
            "description": "NZ 886 088 River Esk at Ruswarp"
          },
          {
            "id": "2",
            "description": "NZ 873 082 River Esk at Briggswath"
          }
        ],
        "purposes": [
          {
            "id": "1",
            "description": "Public Water Supply"
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
        "value": 22.73,
        "units": "Thousand cubic metres per day",
        "points": [
          {
            "id": "1",
            "description": "NZ 886 088 River Esk at Ruswarp"
          },
          {
            "id": "2",
            "description": "NZ 873 082 River Esk at Briggswath"
          }
        ],
        "purposes": [
          {
            "id": "1",
            "description": "Public Water Supply"
          }
        ],
        "implicitLimit": false
      }
    ],
    "aggregates": []
  },
  "definitionOfYear": {
    "startDate": "",
    "endDate": "",
    "inclusive": false
  }
};