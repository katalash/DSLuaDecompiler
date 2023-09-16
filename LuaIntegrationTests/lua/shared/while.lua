function break_test()
    local a = 1
    if a == 0 then
        for i = 1, 10 do
            if a > 5 then
                a = a + 2
                break
            elseif a == 3 then
                a = a + 1
            end
        end
    end
    return a
end 