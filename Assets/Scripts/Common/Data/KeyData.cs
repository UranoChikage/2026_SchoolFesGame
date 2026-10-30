
using System.Collections.Generic;

[System.Serializable]
public class KeyData<T>
{
    public string key;
    public T value;

    public KeyData(string key, T value)
    {
        this.key = key;
        this.value = value;
    }
}

[System.Serializable]
public class KeyDataList<T>
{
    public List<KeyData<T>> keyDataList;
    public KeyDataList()
    {
        keyDataList = new List<KeyData<T>>();
    }

    public KeyDataList(List<KeyData<T>> keyDataList)
    {
        this.keyDataList = keyDataList;
    }
    public void Add(string key, T value)
    {
        keyDataList.Add(new KeyData<T>(key, value));
    }
    public T GetValue(string key)
    {
        foreach (var keyData in keyDataList)
        {
            if (keyData.key == key)
            {
                return keyData.value;
            }
        }
        throw new KeyNotFoundException($"Key '{key}' not found.");
    }
}
