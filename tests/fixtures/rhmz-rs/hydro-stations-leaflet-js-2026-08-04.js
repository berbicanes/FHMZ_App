let timestampAPI = Date.now();
const config = {
    API: {
        meteoStations: '../data/feeds/HidroPodaci.json?t=' + timestampAPI,
    },
    MAP: {
        element: 'map',
        latLng: {lat: 44.1, lng: 17.8},
        zoomLevel: 7.9,
        typeControl: false,
        scrollwheel: false,
        mapType: 'roadmap',
        iconsURL: 'images/icons/',
        capitalCity: {
            title: 'Banja Luka',
            lat: 44.7725,
            lng: 17.186,
            icon: 'images/icons/general-icons/capital_big.png',
            zindex: -1
        }
    }
};


document.addEventListener('DOMContentLoaded', function () {
    let map = L.map(config.MAP.element, {
        minZoom: 7.9,
        maxBounds: L.latLngBounds(L.latLng(42.374778,15.31494), L.latLng(45.583290, 20.192871)),
    }).setView(config.MAP.latLng, config.MAP.zoomLevel);
    map.scrollWheelZoom.disable();

    $("#map").bind('mousewheel DOMMouseScroll', function (event) {
        event.stopPropagation();
        if (event.ctrlKey == true) {
            event.preventDefault();
            map.scrollWheelZoom.enable();
            $('#map').removeClass('map-scroll');
            setTimeout(function(){
                map.scrollWheelZoom.disable();
            }, 1000);
        } else {
            map.scrollWheelZoom.disable();
            $('#map').addClass('map-scroll');
        }

    });

    $(window).bind('mousewheel DOMMouseScroll', function (event) {
        $('#map').removeClass('map-scroll');
    })
    $('#example').DataTable({
        ajax: {
            dataSrc: '',
            url: config.API.meteoStations,
        },
        "columns": [
            { "data": "Станица" },
            { "data": "Вријеме" },
            // { "data": "tendention", "defaultContent": "" },
            { "data": "Водостај" },
            { "data": "Температура Воде" },
        ],
        "language": {
            "url": "../js/Datatable/Serbian.json"
        }
    });

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    }).addTo(map);
    let data = [];
    let markerArr = [];

    axios.get(`${config.API.meteoStations}`).then(rs => {
        data = rs.data;
        data.forEach(item => {
            let marker = L.marker(
                [item['Lat'], item['Lon']],
                {
                    icon:
                        L.icon({
                            iconUrl: "../assets/img/icons/hidro.png"
                        }),
                    item: item,
                }
            ).addTo(map)
                .on('click', markerOnClick);
            markerArr.push(marker);
        })
    })

    function markerOnClick(e) {
        console.log(e)
        var id = e.target.options.description;
        $(".modal-content").html('<div class="modal-header p-2">\n' +
            '                                <h5 class="modal-title modal-title"></h5>\n' +
            '                                <button type="button" class="close" data-dismiss="modal" onclick="$(\'#emptymodal\').modal(\'hide\');" aria-label="Close">\n' +
            '                                    <span aria-hidden="true">&times;</span>\n' +
            '                                </button>\n' +
            '                            </div>' + `<h3>${e.target.options.item['Станица']}</h3>
<p>Аутоматска хидролошка станица ${e.target.options.item['Станица']}</p>
`);
        $('#emptymodal').modal('show');
    }
});



