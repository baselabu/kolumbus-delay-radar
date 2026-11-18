using 'main.bicep'

param budgetEmail = 'you@example.com'          // where the budget alerts are sent
param adminObjectId = '00000000-0000-0000-0000-000000000000'   // az ad signed-in-user show --query id -o tsv
