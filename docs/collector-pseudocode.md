START (once):
  read the settings (Entur URL, poll interval, database connection)
  create one requestorId and keep it for the whole run
  connect to the database, create the tables if they are missing

REPEAT every 30 seconds, until told to stop:
  ask Entur for the data (send our requestorId)
  for each trip in the answer:
    for each stop in the trip:
      build a row: key = (day, trip, order) + line, stop, aimed/expected/actual times, cancelled
  save all rows: insert new ones, overwrite existing ones (upsert)
  log "saved N rows in X ms"
  wait 30 seconds

IF something goes wrong:
  Entur does not answer → log the error, do NOT crash, try again next round
  a trip has broken data → skip that trip, keep the others, log how many were skipped

STOP (once):
  finish the current round, then exit cleanly (Ctrl+C or Kubernetes stopping the pod)