
result=$(curl --request GET 'http://localhost:7071/api/kanaliiga_script' --header 'Content-Type: application/json' -d '@Debug.json')
echo $result
