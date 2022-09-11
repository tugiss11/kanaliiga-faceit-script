


const gridOptions = {

    // each entry here represents one column
    columnDefs: [
        // uses the default column properties
        { headerName: 'Steam ID', field: 'id' },
        { headerName: 'Team', field: 'team_name' },
        { headerName: 'Faceit Name', field: 'faceit_name' },
        { headerName: 'Faceit Elo', field: 'faceit_elo' },
        { headerName: 'Faceit Matches', field: 'faceit_matches' },
        { headerName: 'Public', field: 'is_public' },
        { headerName: '2 wk hours', field: 'playtime_2weeks_hours' },
        { headerName: 'Alltime hours', field: 'playtime_forever_hours' }],


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
fetch('playersdata.json')
    .then(response => response.json())
    .then(data => {
        // load fetched data into grid
        gridOptions.api.setRowData(data);
    });
