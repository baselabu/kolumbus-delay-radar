// Subscription scope: the node VM, disks and load balancer are billed in the AKS-managed
// MC_ resource group, so a budget on rg-delay-radar alone would miss almost all costs.
targetScope = 'subscription'

@description('E-mail for budget alerts')
param budgetEmail string

@description('Monthly budget in USD')
param amount int

@description('First day of the current month; budgets must start on the 1st')
param startDate string = utcNow('yyyy-MM-01')

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'budget-delay-radar'
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
    }
    notifications: {
      actual50: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50 // percent of amount
        contactEmails: [budgetEmail]
      }
      actual80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        contactEmails: [budgetEmail]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted' // warns before we get there, based on the current spend rate
        contactEmails: [budgetEmail]
      }
    }
  }
}
