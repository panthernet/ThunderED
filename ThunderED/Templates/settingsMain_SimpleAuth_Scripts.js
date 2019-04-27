var $table = $('#table');
var $remove = $('#delButton');
var selections = [];
var maxId = 0;

function groupFormatter(value, row) {
    return `<a class="groupValue" data-name="Group" data-value="` +
        value +
        `" data-pk="` +
        row.Id +
        `">${value}</a>`;
}

function getIdSelections() {
    return $.map($table.bootstrapTable('getSelections'),
        function (row) {
            return row.Id;
        });
}

function responseHandler(res) {
    $.each(res.rows,
        function (i, row) {
            row.state = false;
        });
    return res;
}

function updateEditables() {
    $('.groupValue').editable({
        type: 'select',
        source: "{saGroupList}",
        success: function (a, newValue) {
	        debugger;
            $(this).attr("data-value", newValue);
            var pk = $(this)[0].getAttribute("data-pk");
            var row = $table.bootstrapTable('getRowByUniqueId', pk);
            row.Group = newValue;
            $table.bootstrapTable('updateByUniqueId',
	            {
		            id: pk,
		            row: row
	            });
        }
    });
}

$(function () {

    $table.on('all.bs.table',
        function (e, name, args) {
            updateEditables();
            //TODO optimize? too many calls
            let ids = $table.bootstrapTable('getData').map(a => a.Id);
            maxId = Math.max.apply(Math, ids);
        });

    $table.on('check.bs.table uncheck.bs.table ' +
        'check-all.bs.table uncheck-all.bs.table',
        function () {
            $remove.prop('disabled', !$table.bootstrapTable('getSelections').length);

            // save your data, here just save the current page
            selections = getIdSelections();
            // push or splice the selections if you want to save all data selections
        });

    $('#addButton').click(function () {
        maxId++;
        debugger;
        $table.bootstrapTable('insertRow',
            {
                index: 0,
                row: {
                    Id: maxId,
                    Name: '',
                    Group: '',
                    Roles: ''
                }
            });
        updateEditables();
    });

    $('#delButton').click(function () {
        var ids = getIdSelections();
        $table.bootstrapTable('remove',
            {
                field: 'Id',
                values: ids
            });
        $remove.prop('disabled', true);
    });

    $('#saveButton').on('click',
        function (event) {
            event.preventDefault();
            $('#smError').addClass("d-none");

            debugger;

            var str2 = encodeURIComponent(JSON.stringify($table.bootstrapTable('getData')));

            $("#smError").load('{postSimplifiedAuthUrl}' + str2,
                function (result) {
                    if (result.toLowerCase().startsWith('error')) {
                        $('#smError').removeClass("d-none");
                        $('#smError').text(result);
                    }
                });
        });

});