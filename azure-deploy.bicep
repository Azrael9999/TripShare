param location string = resourceGroup().location
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
param deploymentTier string = 'Free'
@allowed([
  'Free'
  'Paid'
])
param appInsightsName string = 'tripshare-ai'
param storageName string
param redisName string = 'tripshare-cache'
param communicationName string = 'tripshare-comms'
param apiName string = 'tripshare-api'
param sku string = 'B1'
@secure()
param jwtSigningKey string
param corsAllowedOrigins array = []
param emailMode string = 'DevFile'
param emailFromName string = 'HopTrip'
param emailFromEmail string = ''
@secure()
param emailAcsConnectionString string = ''
param emailAcsFromEmail string = ''
param emailSmtpHost string = ''
param emailSmtpPort int = 587
param emailSmtpUseSsl bool = true
param emailSmtpUsername string = ''
@secure()
param emailSmtpPassword string = ''
param smsProvider string = 'TextLk'
@secure()
param smsTextLkApiKey string = ''
param smsTextLkSenderId string = ''
param smsTextLkEndpoint string = 'https://app.text.lk/api/v3/sms/send'
@secure()
param smsAcsConnectionString string = ''
param smsAcsSender string = ''
param telemetryApiKey string = ''
param backgroundJobsProvider string = 'StorageQueue'

var sqlServerName = '${apiName}-sql'
var dbName = 'TripShareDb'
var queueName = 'tripshare-jobs'
var poisonQueue = 'tripshare-jobs-poison'
var usePaid = toLower(deploymentTier) == 'paid'
var planSkuName = usePaid ? sku : 'F1'
var planSkuTier = usePaid ? 'Basic' : 'Free'
var sqlSkuName = usePaid ? 'GP_S_Gen5_2' : 'Basic'
var enableRedis = usePaid
var enableCommunication = usePaid
var enableAppInsights = usePaid

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
  dependsOn: [storage]
  if: enableRedis
}

resource comms 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: communicationName
  location: location
  properties: {
    dataLocation: 'UnitedStates'
  }
  if: enableCommunication
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
    name: sqlSkuName
  }
  properties: {
    readScale: 'Disabled'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${apiName}-plan'
  location: location
  sku: {
    name: planSkuName
    tier: planSkuTier
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
        { name: 'BackgroundJobs__Provider', value: backgroundJobsProvider }
        { name: 'BackgroundJobs__StorageQueue__ConnectionString', value: storage.listKeys().keys[0].value }
        { name: 'BackgroundJobs__StorageQueue__QueueName', value: queueName }
        { name: 'BackgroundJobs__StorageQueue__PoisonQueueName', value: poisonQueue }
        { name: 'Cache__RedisConnection', value: enableRedis ? '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False' : '' }
        { name: 'ApplicationInsights__ConnectionString', value: enableAppInsights ? appInsights.properties.ConnectionString : '' }
        { name: 'Jwt__SigningKey', value: jwtSigningKey }
        { name: 'Email__Mode', value: emailMode }
        { name: 'Email__FromName', value: emailFromName }
        { name: 'Email__FromEmail', value: emailFromEmail }
        { name: 'Email__Acs__ConnectionString', value: emailAcsConnectionString }
        { name: 'Email__Acs__FromEmail', value: emailAcsFromEmail }
        { name: 'Email__SmtpHost', value: emailSmtpHost }
        { name: 'Email__SmtpPort', value: string(emailSmtpPort) }
        { name: 'Email__SmtpUseSsl', value: string(emailSmtpUseSsl) }
        { name: 'Email__SmtpUser', value: emailSmtpUsername }
        { name: 'Email__SmtpPass', value: emailSmtpPassword }
        { name: 'Sms__Provider', value: smsProvider }
        { name: 'Sms__TextLk__ApiKey', value: smsTextLkApiKey }
        { name: 'Sms__TextLk__SenderId', value: smsTextLkSenderId }
        { name: 'Sms__TextLk__Endpoint', value: smsTextLkEndpoint }
        { name: 'Sms__Acs__ConnectionString', value: smsAcsConnectionString }
        { name: 'Sms__Acs__Sender', value: smsAcsSender }
        { name: 'Telemetry__ApiKey', value: telemetryApiKey }
        { name: 'Deployment__Tier', value: deploymentTier }
        for origin in corsAllowedOrigins: {
          name: 'Cors__AllowedOrigins__${indexOf(corsAllowedOrigins, origin)}'
          value: origin
        }
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
  if: enableAppInsights
}

output storageConnection string = storage.listKeys().keys[0].value
output redisConnection string = enableRedis ? '${redis.properties.hostName}:6380,password=${redis.listKeys().primaryKey},ssl=True,abortConnect=False' : ''
output sqlConnection string = 'Server=tcp:${sqlServer.name}.database.windows.net,1433;Database=${dbName};User ID=${sqlAdminLogin};Password=<redacted>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
output appInsightsConnection string = enableAppInsights ? appInsights.properties.ConnectionString : ''
