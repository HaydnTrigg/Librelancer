if ModalData['Title'] ~= nil then
    GetElement('title').Text = ModalData.Title
end
if ModalData['Content'] ~= nil then
    GetElement('content').Text = ModalData.Content
end
GetElement('close'):OnClick(function()
	CloseModal({ Result = 'cancel' })
end)
if ModalData['Buttons'] == 'ok' then
	GetElement('ok_ok').Visible = true
end
GetElement('ok_ok'):OnClick(function()
	CloseModal({ Result = 'ok' })
end)




