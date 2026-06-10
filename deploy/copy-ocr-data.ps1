$server = "ken@ns540399.ip-144-217-180.net"
$localRoot = "D:\Dev\YearbookData\yearbook-data"

Get-ChildItem $localRoot -Directory -Filter "Bonhomie-*" | ForEach-Object {
    $yearbook = $_.Name
    ssh $server "mkdir -p /opt/yearbook-data/$yearbook"
    scp -r "$($_.FullName)\text" "${server}:/opt/yearbook-data/$yearbook/"
}