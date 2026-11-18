# Usage: source scripts/nettest.sh; nettest postgres 5432
# nettest HOST PORT: start a throwaway "stranger" pod (no app labels) and try a TCP connection
nettest() {
  kubectl -n delay-radar run "nettest-$RANDOM" --rm -i --restart=Never --quiet --image=postgres:17.11 \
    --overrides="{\"spec\":{\"securityContext\":{\"runAsNonRoot\":true,\"runAsUser\":999,\"seccompProfile\":{\"type\":\"RuntimeDefault\"}},\"containers\":[{\"name\":\"t\",\"image\":\"postgres:17.11\",\"command\":[\"bash\",\"-c\",\"timeout 5 bash -c 'exec 3<>/dev/tcp/$1/$2' 2>/dev/null && echo 'REACHED $1:$2' || echo 'BLOCKED $1:$2'\"],\"securityContext\":{\"allowPrivilegeEscalation\":false,\"capabilities\":{\"drop\":[\"ALL\"]}}}]}}"
}
