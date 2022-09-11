


const gridOptions = {

    // each entry here represents one column
    columnDefs: [
        // uses the default column properties
        { headerName: 'Team name', field: 'name' },
        { headerName: 'Company', field: 'company_name' },
        { headerName: 'Avg Elo', field: 'avgElo' },
        { headerName: 'Median Elo', field: 'medianElo' },
        { headerName: 'Total games', field: 'totalGames' },
        { headerName: 'AvgLast2WeekHours', field: 'avgLast2Week' },
        { headerName: 'AvgAlltimeHours', field: 'avgTotalHours' },
        { headerName: 'Has Faceit Matches', field: 'playersWithFaceitMatches' }],


    // default col def properties get applied to all columns
    defaultColDef: { sortable: true, filter: true },

    rowSelection: 'multiple', // allow rows to be selected
    animateRows: true, // have rows animate to new positions when sorted

    // example event handler
    onCellClicked: params => {
        console.log('cell was clicked', params)
    }
};

const eGridDiv = document.getElementById("myGrid");
new agGrid.Grid(eGridDiv, gridOptions);
fetch('teamsdata.json')
    .then(response => response.json())
    .then(data => {
        // load fetched data into grid
        gridOptions.api.setRowData(data);
    });
