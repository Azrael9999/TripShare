param location string = resourceGroup().location
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
param appInsightsName string = 'tripshare-ai'
param storageName string
param redisName string
param communicationName string
param apiName string = 'tripshare-api'
param sku string = 'B1'

var sqlServerName = '${apiName}-sql'
var dbName = 'TripShareDb'
var queueName = 'tripshare-jobs'
var poisonQueue = 'tripshare-jobs-poison'

resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource queues 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  name: '${storage.name}/default/${queueName}'
  dependsOn: [storage]
}

resource poisonQueues 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  name: '${storage.name}/default/${poisonQueue}'
  dependsOn: [storage]
}

resource redis 'Microsoft.Cache/Redis@2023-08-01' = {
  name: redisName
  location: location
  sku: {
    name: 'Basic'
    family: 'C'
    capacity: 0
  }
  properties: {
    enableNonSslPort: false
  }
}

resource comms 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationName
  location: location
  properties: {
    dataLocation: 'UnitedStates'
  }
}

resource sqlServer 'Microsoft.Sql/servers@2022-11-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2022-11-01-preview' = {
  name: '${sqlServer.name}/${dbName}'
  location: location
  sku: {
    name: 'GP_S_Gen5_2'
  }
  properties: {
    readScale: 'Disabled'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${apiName}-plan'
  location: location
  sku: {
    name: sku
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: apiName
  location: location
  kind: 'app,linux,container'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|<your-registry>/tripshare-api:latest'
      appSettings: [
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__DefaultConnection', value: 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Database=${dbName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;' }
        { name: 'BackgroundJobs__Provider', value: 'StorageQueue' }
        { name: 'BackgroundJobs__StorageQueue__ConnectionString', value: storage.listKeys().keys[0].value }
        { name: 'BackgroundJobs__StorageQueue__QueueName', value: queueName }
        { name: 'BackgroundJobs__StorageQueue__PoisonQueueName', value: poisonQueue }
        { name: 'Cache__RedisConnection', value: redis.properties.hostName != '' ? '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False' : '' }
        { name: 'ApplicationInsights__ConnectionString', value: appInsights.properties.ConnectionString }
      ]
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

output storageConnection string = storage.listKeys().keys[0].value
output redisConnection string = '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False'
output sqlConnection string = 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Database=${dbName};User ID=${sqlAdminLogin};Password=<redacted>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
output appInsightsConnection string = appInsights.properties.ConnectionString
