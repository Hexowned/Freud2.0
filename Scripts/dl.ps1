$client = new-object System.Net.WebClient
$client.DownloadFile("","Freud.zip")
$client.DownloadFile("","FreudResources.zip")