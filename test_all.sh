
result=$(curl --request GET 'http://localhost:7071/api/kanaliiga_script' --header 'Content-Type: application/json' -d '@Teams_masters.json')
echo $result
