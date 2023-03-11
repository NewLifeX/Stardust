export const tableSearchConfig = [
  {
    itemType: "datePicker",
    name: "dtStart$dtEnd",
    displayName: "时间范围",
    showInSearch: true,
    options: { type: "daterange", setDefaultValue: false },
  },
  {
    name: "category",
    displayName: "类别",
    showInSearch: true,
    itemType: "select",
    url: [
      {
        label: "全部",
        value: "",
      },
      {
        label: "物联网",
        value: "物联网",
      },
      {
        label: "基础平台",
        value: "基础平台",
      },
      {
        label: "魔方",
        value: "魔方",
      },
      {
        label: "定位",
        value: "定位",
      },
      {
        label: "Zero脚手架",
        value: "Zero脚手架",
      },
      {
        label: "新生命",
        value: "新生命",
      },
    ],
  },
  {
    name: "appId",
    displayName: "应用名称",
    showInSearch: true,
    itemType: "select",
    url: "/Registry/App/AppSearch",
    options: {
      method: "get",
      labelField: "displayName",
      valueField: "id",
      remote: true,
      keyField: "key",
    },
  },
  {
    name: "enable",
    displayName: "状态",
    showInSearch: true,
    itemType: "select",
    url: [
      {
        label: "启用",
        value: 1,
      },
      {
        label: "禁用",
        value: 0,
      },
    ],
  },
  {
    name: "Q",
    displayName: "",
    showInSearch: true,
    options: {
      placeholder: "请输入关键字",
    },
  },
];
