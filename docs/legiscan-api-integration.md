# LegiScan API Integration

Source reviewed: `LegiScan_API_User_Manual.pdf`, revision `20250317`, API v1.91.

## Compliance Notes

- Pull API calls use `https://api.legiscan.com/?key=APIKEY&op=OPERATION&PARAMS`.
- Every JSON response must be checked for `status: "OK"` before reading payload data. `status: "ERROR"` may include `alert.message`.
- Public service keys have a monthly limit of 30,000 queries.
- LegiScan recommends loading or maintaining bill state by comparing `change_hash` values, then calling `getBill` only for changed bills.
- `getMonitorListRaw` is the lowest-cost monitor-list check for change detection. SIMS uses this before any bill detail calls.
- SIMS enforces a local cap of 50 active monitored bills.

## SIMS Approach

1. Store monitored bills in `legiscan_tracked_bills`.
2. Use `setMonitor` only when a user adds or removes monitored bills.
3. Use `getMonitorListRaw` during sync to compare `change_hash` values.
4. Use `getBill` only for new bills or bills whose hash changed.
5. Create legal source scan results for changed bills so changes enter the existing review workflow.

## Configuration

Set the API key in configuration:

```json
"LegiScan": {
  "BaseUrl": "https://api.legiscan.com",
  "ApiKey": "YOUR_LEGISCAN_API_KEY",
  "MaxMonitoredBills": 50,
  "MonthlyQueryLimit": 30000
}
```
